using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Migrator;

public static partial class Extends
{
    public static string ComputeHash(string sql) => Convert
        .ToHexString(SHA256
            .HashData(Encoding
                .UTF8
                .GetBytes(sql)));
    
    public static bool CheckMigrations(IList<MigrationScript> migrationScripts, IList<MigrationHistory> migrationHistories, int lastAppliedMigrationOrder)
    {
        return migrationScripts.Count(x => x.Order <= lastAppliedMigrationOrder) 
               == migrationHistories.Count
               && 0 == (migrationScripts
                   .Where(x => x.Order <= lastAppliedMigrationOrder)
                   .Select(x => x.Order ^ x.Hash
                       .Select((c, i) => i * c)
                       .Aggregate(0, (acc, y) => acc ^ y))
                   .Aggregate(0, (acc, x) => acc ^ x)
               ^ migrationHistories
                   .Select(x => x.Order ^ x.Hash
                       .Select((c, i) => i * c)
                       .Aggregate(0, (acc, y) => acc ^ y))
                   .Aggregate(0, (acc, x) => acc ^ x));
    }
    
    public static readonly Regex MigrationRegex = MyRegex();
    
    public static MigrationScript[] GetMigrationScripts(string scriptsPath)
    {
        return Directory
            .GetFiles(scriptsPath, "*.sql")
            .Select(f => new
            {
                File = f,
                Match = MigrationRegex.Match(Path.GetFileName(f))
            })
            .Where(x => x.Match.Success)
            .Select(x => new MigrationScript(
                int.Parse(x.Match.Groups["order"].Value),
                Path.GetFileName(x.File),
                x.Match.Groups["desc"].Value,
                File.ReadAllText(x.File)
            ))
            .OrderBy(x => x.Order)
            .ToArray();
    }

    [GeneratedRegex(@"^(?<order>\d{4})\.(?<desc>.+)\.sql$", RegexOptions.IgnoreCase, "ru-RU")]
    private static partial Regex MyRegex();
}