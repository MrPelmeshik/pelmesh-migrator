using Npgsql;

namespace Migrator;

public static class Program
{
    private const int MaxErrorTextLength = 4000;

    public static int Main(string[] args)
    {
        ApplySummary? summary = null;
        Config? cfg = null;
        try
        {
            cfg = new Config(args);

            if (!cfg.VerifyHash)
                Console.Error.WriteLine("WARNING: проверка хешей отключена (--no-verify-hash)");

            var databases = Extends.DiscoverDatabases(cfg);
            summary = new ApplySummary { Databases = databases.Length };

            foreach (var dbMigrations in databases)
            {
                ApplyDatabase(cfg, dbMigrations, summary);
            }

            Console.WriteLine(summary.ToString());
            return ExitCodes.Resolve(summary, cfg.OnError, cfg.FailOnScriptErrors);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            if (summary != null)
                Console.WriteLine(summary.ToString());
            return 1;
        }
    }

    /// <summary>
    /// Применяет миграции к одной БД с учётом txn / on-error / verify-hash.
    /// </summary>
    private static void ApplyDatabase(Config cfg, DatabaseMigrations dbMigrations, ApplySummary summary)
    {
        var db = dbMigrations.Database;
        var migrationScripts = dbMigrations.Scripts;
        var migrationHistories = GetMigrationHistories(cfg, db);
        var appliedOrders = migrationHistories.Select(x => x.Order).ToHashSet();

        var mismatch = Extends.DescribeMigrationMismatch(migrationScripts, migrationHistories, cfg.VerifyHash);
        if (mismatch != null)
            throw new Exception($"Миграции для БД {db}: {mismatch}");

        if (!Extends.CheckMigrations(migrationScripts, migrationHistories, cfg.VerifyHash))
            throw new Exception($"Миграции в базе данных {db} не совпадают со скриптами");

        var pending = migrationScripts
            .Where(x => !appliedOrders.Contains(x.Order))
            .OrderBy(x => x.Order)
            .ToArray();

        summary.Skipped += migrationScripts.Length - pending.Length;

        if (pending.Length == 0)
            return;

        using var conn = new NpgsqlConnection(GetConnectionString(cfg, db));
        conn.Open();

        switch (cfg.Transaction)
        {
            case TransactionMode.All:
                ApplyAllInOneTransaction(cfg, conn, pending, summary);
                break;
            case TransactionMode.PerFile:
                ApplyPerFile(cfg, conn, pending, summary);
                break;
            case TransactionMode.None:
                ApplyWithoutTransaction(cfg, conn, pending, summary);
                break;
            default:
                throw new Exception($"Неизвестный режим транзакций: {cfg.Transaction}");
        }
    }

    /// <summary>
    /// Одна транзакция на все pending-файлы (on-error=fail).
    /// </summary>
    private static void ApplyAllInOneTransaction(
        Config cfg,
        NpgsqlConnection conn,
        MigrationScript[] pending,
        ApplySummary summary)
    {
        using var trans = conn.BeginTransaction();
        try
        {
            foreach (var script in pending)
            {
                ExecuteSql(conn, trans, script.Sql);
                InsertHistory(conn, trans, cfg, script, MigrationStatuses.Applied, errorText: null);
                summary.Applied++;
            }

            trans.Commit();
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Транзакция на файл; при continue — statement-level + savepoints.
    /// </summary>
    private static void ApplyPerFile(
        Config cfg,
        NpgsqlConnection conn,
        MigrationScript[] pending,
        ApplySummary summary)
    {
        foreach (var script in pending)
        {
            if (cfg.OnError == OnErrorMode.Continue)
            {
                ApplyFileWithContinue(cfg, conn, script, summary, useTransaction: true);
                continue;
            }

            using var trans = conn.BeginTransaction();
            try
            {
                ExecuteSql(conn, trans, script.Sql);
                InsertHistory(conn, trans, cfg, script, MigrationStatuses.Applied, errorText: null);
                trans.Commit();
                summary.Applied++;
            }
            catch (Exception ex)
            {
                trans.Rollback();
                throw new Exception($"файл {script.FileName}: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Применение без обёртки транзакцией.
    /// </summary>
    private static void ApplyWithoutTransaction(
        Config cfg,
        NpgsqlConnection conn,
        MigrationScript[] pending,
        ApplySummary summary)
    {
        foreach (var script in pending)
        {
            if (cfg.OnError == OnErrorMode.Continue)
            {
                ApplyFileWithContinue(cfg, conn, script, summary, useTransaction: false);
                continue;
            }

            try
            {
                ExecuteSql(conn, transaction: null, script.Sql);
                InsertHistory(conn, transaction: null, cfg, script, MigrationStatuses.Applied, errorText: null);
                summary.Applied++;
            }
            catch (Exception ex)
            {
                throw new Exception($"файл {script.FileName}: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Continue: statement-level внутри файла; ошибки не останавливают следующие statements/файлы.
    /// </summary>
    private static void ApplyFileWithContinue(
        Config cfg,
        NpgsqlConnection conn,
        MigrationScript script,
        ApplySummary summary,
        bool useTransaction)
    {
        NpgsqlTransaction? trans = useTransaction ? conn.BeginTransaction() : null;
        var errors = new List<string>();

        try
        {
            var statements = SqlStatementSplitter.Split(script.Sql);
            if (statements.Count == 0)
                statements = [script.Sql.Trim()];

            for (var i = 0; i < statements.Count; i++)
            {
                var savepoint = useTransaction ? $"sp_{i}" : null;
                if (savepoint != null)
                    ExecuteSql(conn, trans, $"SAVEPOINT {savepoint}");

                try
                {
                    ExecuteSql(conn, trans, statements[i]);
                }
                catch (Exception ex)
                {
                    if (savepoint != null)
                        ExecuteSql(conn, trans, $"ROLLBACK TO SAVEPOINT {savepoint}");

                    var message = ex.Message;
                    errors.Add(message);
                    Console.Error.WriteLine(
                        $"ERROR: файл {script.FileName}, statement #{i + 1}: {message}");
                }
            }

            if (errors.Count > 0)
            {
                summary.Errors++;
                InsertHistory(
                    conn,
                    trans,
                    cfg,
                    script,
                    MigrationStatuses.AppliedWithErrors,
                    TruncateError(string.Join("; ", errors)));
            }
            else
            {
                InsertHistory(conn, trans, cfg, script, MigrationStatuses.Applied, errorText: null);
                summary.Applied++;
            }

            trans?.Commit();
        }
        catch
        {
            trans?.Rollback();
            throw;
        }
        finally
        {
            trans?.Dispose();
        }
    }

    private static void ExecuteSql(NpgsqlConnection conn, NpgsqlTransaction? transaction, string sql)
    {
        using var cmd = transaction == null
            ? new NpgsqlCommand(sql, conn)
            : new NpgsqlCommand(sql, conn, transaction);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Пишет запись в migration_history (sql — по --store-sql).
    /// </summary>
    private static void InsertHistory(
        NpgsqlConnection conn,
        NpgsqlTransaction? transaction,
        Config cfg,
        MigrationScript script,
        string status,
        string? errorText)
    {
        const string sql = """
                           insert into migration_history
                               (order_number, file_name, name, sql, hash, status, applied_at, error_text)
                           values
                               (@order_number, @file_name, @name, @sql, @hash, @status, now(), @error_text)
                           """;

        using var cmd = transaction == null
            ? new NpgsqlCommand(sql, conn)
            : new NpgsqlCommand(sql, conn, transaction);

        cmd.Parameters.AddWithValue("order_number", script.Order);
        cmd.Parameters.AddWithValue("file_name", script.FileName);
        cmd.Parameters.AddWithValue("name", script.Name);
        cmd.Parameters.AddWithValue("sql", cfg.StoreSql ? script.Sql : "");
        cmd.Parameters.AddWithValue("hash", script.Hash);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("error_text", (object?)errorText ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private static string TruncateError(string message)
        => message.Length <= MaxErrorTextLength
            ? message
            : message[..MaxErrorTextLength];

    private static IList<MigrationHistory> GetMigrationHistories(Config cfg, string dbName)
    {
        using var conn = new NpgsqlConnection(GetConnectionString(cfg, dbName));
        conn.Open();

        HistorySchema.Ensure(conn);
        return ReadMigrationHistories(conn);
    }

    private static List<MigrationHistory> ReadMigrationHistories(NpgsqlConnection conn)
    {
        using var cmd = new NpgsqlCommand("""
                                          select order_number
                                          ,      hash
                                          ,      file_name
                                          ,      coalesce(status, 'applied') as status
                                          from   migration_history
                                          """, conn);
        using var reader = cmd.ExecuteReader();

        var migrationHistories = new List<MigrationHistory>();
        while (reader.Read())
        {
            migrationHistories.Add(new MigrationHistory(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? "" : reader.GetString(2),
                reader.IsDBNull(3) ? MigrationStatuses.Applied : reader.GetString(3)
            ));
        }

        return migrationHistories;
    }

    private static string GetConnectionString(Config cfg, string dbName)
    {
        if (string.IsNullOrEmpty(dbName))
            throw new ArgumentNullException(nameof(dbName));

        return $"Host={cfg.Host};Port={cfg.Port};Database={dbName};Username={cfg.User};Password={cfg.Password};";
    }
}
