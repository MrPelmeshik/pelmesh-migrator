using Npgsql;

namespace Migrator;

public static class Program
{
    public static void Main(string[] args)
    {
        var cfg = new Config(args);
        
        var dbs = Directory
            .GetDirectories(cfg.MigrationDirectory)
            .Select(Path.GetFileName)
            .Where(fileName => !string.IsNullOrEmpty(fileName))
            .Cast<string>()
            .ToArray();
        
        if (dbs.Length == 0)
            throw new Exception("Нет баз данных");

        foreach (var db in dbs)
        {
            var migrationDir = Path.Combine(cfg.MigrationDirectory, db);
            var migrationScripts = Extends.GetMigrationScripts(migrationDir);
            var migrationHistories = GetMigrationHistories(cfg, db);
            var lastAppliedMigrationOrder = migrationHistories.Count == 0
                ? 0
                : migrationHistories.Max(x => x.Order);

            if (!Extends.CheckMigrations(migrationScripts, migrationHistories, lastAppliedMigrationOrder))
            {
                throw new Exception($"Миграции в директории {migrationDir} не совпадают с миграцией в базе данных {db}");
            }

            using var conn = new NpgsqlConnection(GetConnectionString(cfg, db));
            conn.Open();
            using var trans = conn.BeginTransaction();
            try
            {
                foreach (var migrationScript in migrationScripts.Where(x => x.Order > lastAppliedMigrationOrder))
                {
                    using var cmd = new NpgsqlCommand(migrationScript.Sql, conn, trans);
                    cmd.ExecuteNonQuery();

                    using var cmd2 = new NpgsqlCommand($"""
                                                       insert into migration_history (order_number, file_name, name, sql, hash)
                                                       values ( @{nameof(migrationScript.Order)}
                                                       ,        @{nameof(migrationScript.FileName)}
                                                       ,        @{nameof(migrationScript.Name)}
                                                       ,        @{nameof(migrationScript.Sql)}
                                                       ,        @{nameof(migrationScript.Hash)})
                                                       """, conn, trans);
                    cmd2.Parameters.AddWithValue(nameof(migrationScript.Order), migrationScript.Order);
                    cmd2.Parameters.AddWithValue(nameof(migrationScript.FileName), migrationScript.FileName);
                    cmd2.Parameters.AddWithValue(nameof(migrationScript.Name), migrationScript.Name);
                    cmd2.Parameters.AddWithValue(nameof(migrationScript.Sql), migrationScript.Sql);
                    cmd2.Parameters.AddWithValue(nameof(migrationScript.Hash), migrationScript.Hash);
                    cmd2.ExecuteNonQuery();
                }
                
                trans.Commit();
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }
    }
    
    private static void CreateTableIfNotExists(NpgsqlConnection conn)
    {
        using var cmd = new NpgsqlCommand("""
                                          create table if not exists migration_history (
                                              id serial primary key,
                                              order_number int not null,
                                              file_name text not null,
                                              name text not null,
                                              sql text not null,
                                              hash text not null
                                          );
                                          """, conn);
        cmd.ExecuteNonQuery();
    }

    private static List<MigrationHistory> GetMigrationHistories(NpgsqlConnection conn)
    {
        using var cmd = new NpgsqlCommand("""
                                           select  order_number
                                           ,       hash
                                           from    migration_history
                                           """, conn);
        using var reader = cmd.ExecuteReader();
        
        var migrationHistories = new List<MigrationHistory>();
        while (reader.Read())
        {
            migrationHistories.Add(new MigrationHistory(
                reader.GetInt32(0),
                reader.GetString(1)
            ));
        }
        
        return migrationHistories;
    }

    private static IList<MigrationHistory> GetMigrationHistories(Config cfg, string dbName)
    {
        using var conn = new NpgsqlConnection(GetConnectionString(cfg, dbName));
        conn.Open();
        
        CreateTableIfNotExists(conn);

        return GetMigrationHistories(conn);
    }

    private static string GetConnectionString(Config cfg, string dbName)
    {
        if (string.IsNullOrEmpty(dbName))
            throw new ArgumentNullException(nameof(dbName));

        return $"Host={cfg.Host};Port={cfg.Port};Database={dbName};Username={cfg.User};Password={cfg.Password};";
    }
}