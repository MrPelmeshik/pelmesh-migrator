using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Migrator;

/// <summary>
/// Вспомогательные методы: хеш, regex, чтение скриптов, проверка истории.
/// </summary>
public static partial class Extends
{
    /// <summary>
    /// Regex для nested/flat: номер + описание (БД не из имени).
    /// </summary>
    public static readonly Regex MigrationRegex = NestedOrFlatRegex();

    /// <summary>
    /// Regex для by-filename: номер + database + описание.
    /// </summary>
    public static readonly Regex ByFilenameRegex = ByFilenameGeneratedRegex();

    /// <summary>
    /// Считает SHA-256 от текста SQL (hex uppercase).
    /// </summary>
    public static string ComputeHash(string sql) => Convert
        .ToHexString(SHA256
            .HashData(Encoding
                .UTF8
                .GetBytes(sql)));

    /// <summary>
    /// Сверяет уже применённые скрипты с историей.
    /// При verifyHash=false сравнивает только наличие order.
    /// </summary>
    public static bool CheckMigrations(
        IList<MigrationScript> migrationScripts,
        IList<MigrationHistory> migrationHistories,
        bool verifyHash = true)
    {
        var scriptsByOrder = migrationScripts.ToDictionary(x => x.Order);
        var historiesByOrder = migrationHistories.ToDictionary(x => x.Order);

        foreach (var history in migrationHistories)
        {
            if (!scriptsByOrder.TryGetValue(history.Order, out var script))
                return false;

            if (verifyHash && !string.Equals(script.Hash, history.Hash, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // История не должна содержать order, которых нет в файлах (уже проверено выше).
        // Файлы с order вне истории — это pending, допустимы.
        _ = historiesByOrder;
        return true;
    }

    /// <summary>
    /// Возвращает детальное описание расхождений для fail-сообщения.
    /// </summary>
    public static string? DescribeMigrationMismatch(
        IList<MigrationScript> migrationScripts,
        IList<MigrationHistory> migrationHistories,
        bool verifyHash)
    {
        var scriptsByOrder = migrationScripts.ToDictionary(x => x.Order);

        foreach (var history in migrationHistories)
        {
            if (!scriptsByOrder.TryGetValue(history.Order, out var script))
            {
                return $"В каталоге отсутствует файл для order={history.Order}" +
                       (string.IsNullOrEmpty(history.FileName) ? "" : $", file_name={history.FileName}");
            }

            if (verifyHash && !string.Equals(script.Hash, history.Hash, StringComparison.OrdinalIgnoreCase))
            {
                return $"Расхождение hash для order={script.Order}, file_name={script.FileName}: " +
                       $"expected={history.Hash}, actual={script.Hash}";
            }
        }

        return null;
    }

    /// <summary>
    /// Читает *.sql из каталога по regex layout; unmatched — fail или warning.
    /// </summary>
    public static MigrationScript[] GetMigrationScripts(
        string scriptsPath,
        LayoutMode layout = LayoutMode.Nested,
        bool ignoreUnmatched = false)
    {
        var regex = layout == LayoutMode.ByFilename ? ByFilenameRegex : MigrationRegex;
        var files = Directory.GetFiles(scriptsPath, "*.sql");
        var matched = new List<MigrationScript>();
        var unmatched = new List<string>();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var match = regex.Match(fileName);
            if (!match.Success)
            {
                unmatched.Add(file);
                continue;
            }

            var order = int.Parse(match.Groups["order"].Value);
            var name = match.Groups["desc"].Value;
            string? database = null;
            if (layout == LayoutMode.ByFilename)
                database = match.Groups["database"].Value;

            matched.Add(new MigrationScript(
                order,
                fileName,
                name,
                File.ReadAllText(file),
                database));
        }

        if (unmatched.Count > 0)
        {
            var list = string.Join(Environment.NewLine, unmatched.Select(p => $"  - {p}"));
            if (ignoreUnmatched)
            {
                Console.Error.WriteLine($"WARNING: пропущены unmatched *.sql:{Environment.NewLine}{list}");
            }
            else
            {
                throw new Exception($"Найдены *.sql, не прошедшие regex layout={layout}:{Environment.NewLine}{list}");
            }
        }

        EnsureNoDuplicateOrders(matched);

        return matched
            .OrderBy(x => x.Order)
            .ToArray();
    }

    /// <summary>
    /// Дубликаты order в одном наборе → fail до применения.
    /// </summary>
    public static void EnsureNoDuplicateOrders(IEnumerable<MigrationScript> scripts)
    {
        var duplicates = scripts
            .GroupBy(x => x.Order)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        if (duplicates.Length == 0)
            return;

        throw new Exception(
            $"Дубликаты order в наборе миграций: {string.Join(", ", duplicates)}");
    }

    /// <summary>
    /// Собирает наборы скриптов по БД согласно layout.
    /// </summary>
    public static DatabaseMigrations[] DiscoverDatabases(Config cfg)
    {
        return cfg.Layout switch
        {
            LayoutMode.Nested => DiscoverNested(cfg),
            LayoutMode.Flat => DiscoverFlat(cfg),
            LayoutMode.ByFilename => DiscoverByFilename(cfg),
            _ => throw new Exception($"Неизвестный layout: {cfg.Layout}")
        };
    }

    private static DatabaseMigrations[] DiscoverNested(Config cfg)
    {
        var dbs = Directory
            .GetDirectories(cfg.MigrationDirectory)
            .Select(Path.GetFileName)
            .Where(fileName => !string.IsNullOrEmpty(fileName))
            .Cast<string>()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        if (dbs.Length == 0)
            throw new Exception("Нет баз данных (ожидаются подкаталоги в --scripts для layout=nested)");

        return dbs
            .Select(db =>
            {
                var scripts = GetMigrationScripts(
                    Path.Combine(cfg.MigrationDirectory, db),
                    LayoutMode.Nested,
                    cfg.IgnoreUnmatched);
                WarnNestedDatabaseMismatch(db, scripts);
                return new DatabaseMigrations(db, scripts);
            })
            .ToArray();
    }

    /// <summary>
    /// В nested: если имя похоже на by-filename и сегмент БД ≠ имени папки — WARNING.
    /// Routing по-прежнему по папке.
    /// </summary>
    public static IReadOnlyList<string> WarnNestedDatabaseMismatch(
        string folderDatabase,
        IEnumerable<MigrationScript> scripts,
        TextWriter? warningWriter = null)
    {
        warningWriter ??= Console.Error;
        var warnings = new List<string>();

        foreach (var script in scripts)
        {
            var match = ByFilenameRegex.Match(script.FileName);
            if (!match.Success)
                continue;

            var segment = match.Groups["database"].Value;
            if (string.Equals(segment, folderDatabase, StringComparison.Ordinal))
                continue;

            var message =
                $"WARNING: файл {script.FileName}: сегмент БД '{segment}' не совпадает с именем папки '{folderDatabase}' " +
                "(layout=nested — routing по папке, сегмент в имени игнорируется)";
            warnings.Add(message);
            warningWriter.WriteLine(message);
        }

        return warnings;
    }

    private static DatabaseMigrations[] DiscoverFlat(Config cfg)
    {
        var database = cfg.Database
                       ?? throw new Exception("Для --layout flat обязателен --database");

        var scripts = GetMigrationScripts(
            cfg.MigrationDirectory,
            LayoutMode.Flat,
            cfg.IgnoreUnmatched);

        return [new DatabaseMigrations(database, scripts)];
    }

    private static DatabaseMigrations[] DiscoverByFilename(Config cfg)
    {
        var scripts = GetMigrationScripts(
            cfg.MigrationDirectory,
            LayoutMode.ByFilename,
            cfg.IgnoreUnmatched);

        IEnumerable<IGrouping<string, MigrationScript>> groups = scripts
            .GroupBy(x => x.Database!, StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(cfg.Database))
            groups = groups.Where(g => string.Equals(g.Key, cfg.Database, StringComparison.Ordinal));

        var result = groups
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var list = g.OrderBy(x => x.Order).ToArray();
                EnsureNoDuplicateOrders(list);
                return new DatabaseMigrations(g.Key, list);
            })
            .ToArray();

        if (result.Length == 0)
        {
            throw new Exception(string.IsNullOrWhiteSpace(cfg.Database)
                ? "Не найдено ни одной БД в именах файлов (layout=by-filename)"
                : $"Не найдено файлов для --database {cfg.Database} (layout=by-filename)");
        }

        return result;
    }

    [GeneratedRegex(@"^(?<order>\d+)\.(?<desc>.+)\.sql$", RegexOptions.IgnoreCase, "ru-RU")]
    private static partial Regex NestedOrFlatRegex();

    [GeneratedRegex(@"^(?<order>\d+)\.(?<database>[^.]+)\.(?<desc>.+)\.sql$", RegexOptions.IgnoreCase, "ru-RU")]
    private static partial Regex ByFilenameGeneratedRegex();
}
