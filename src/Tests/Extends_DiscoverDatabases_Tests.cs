using Migrator;

namespace Tests;

public class Extends_DiscoverDatabases_Tests
{
    private const string RootDir = "./test_discover";
    private const string EmptySql = "-- empty sql";

    [TearDown]
    public void TearDown() => Cleanup();

    [Test]
    public void Nested_UsesSubdirectories()
    {
        Cleanup();
        Directory.CreateDirectory(Path.Combine(RootDir, "demo_db"));
        File.WriteAllText(Path.Combine(RootDir, "demo_db", "0001.init.sql"), EmptySql);

        var cfg = ConfigFor(LayoutMode.Nested);
        var result = Extends.DiscoverDatabases(cfg);

        Assert.That(result.Length, Is.EqualTo(1));
        Assert.That(result[0].Database, Is.EqualTo("demo_db"));
        Assert.That(result[0].Scripts[0].Order, Is.EqualTo(1));
    }

    [Test]
    public void Flat_RequiresDatabase_AndUsesFlatFiles()
    {
        Cleanup();
        Directory.CreateDirectory(RootDir);
        File.WriteAllText(Path.Combine(RootDir, "1.init.sql"), EmptySql);

        var cfg = ConfigFor(LayoutMode.Flat, database: "appdb");
        var result = Extends.DiscoverDatabases(cfg);

        Assert.That(result.Length, Is.EqualTo(1));
        Assert.That(result[0].Database, Is.EqualTo("appdb"));
        Assert.That(result[0].Scripts[0].Name, Is.EqualTo("init"));
    }

    [Test]
    public void ByFilename_GroupsByDatabaseSegment()
    {
        Cleanup();
        Directory.CreateDirectory(RootDir);
        File.WriteAllText(Path.Combine(RootDir, "001.dq.a.sql"), EmptySql);
        File.WriteAllText(Path.Combine(RootDir, "029.ois.b.sql"), EmptySql);

        var cfg = ConfigFor(LayoutMode.ByFilename);
        var result = Extends.DiscoverDatabases(cfg);

        Assert.That(result.Select(x => x.Database).ToArray(), Is.EqualTo(new[] { "dq", "ois" }));
        Assert.That(result.First(x => x.Database == "dq").Scripts[0].Name, Is.EqualTo("a"));
        Assert.That(result.First(x => x.Database == "ois").Scripts[0].Name, Is.EqualTo("b"));
    }

    [Test]
    public void ByFilename_FilterDatabase()
    {
        Cleanup();
        Directory.CreateDirectory(RootDir);
        File.WriteAllText(Path.Combine(RootDir, "001.dq.a.sql"), EmptySql);
        File.WriteAllText(Path.Combine(RootDir, "029.ois.b.sql"), EmptySql);

        var cfg = ConfigFor(LayoutMode.ByFilename, database: "dq");
        var result = Extends.DiscoverDatabases(cfg);

        Assert.That(result.Length, Is.EqualTo(1));
        Assert.That(result[0].Database, Is.EqualTo("dq"));
    }

    private static Config ConfigFor(LayoutMode layout, string? database = null)
    {
        var args = new List<string>
        {
            "--host", "localhost",
            "--port", "5432",
            "--user", "postgres",
            "--password", "x",
            "--scripts", RootDir,
            "--layout", layout switch
            {
                LayoutMode.Nested => "nested",
                LayoutMode.Flat => "flat",
                LayoutMode.ByFilename => "by-filename",
                _ => throw new ArgumentOutOfRangeException(nameof(layout))
            }
        };
        if (database != null)
        {
            args.Add("--database");
            args.Add(database);
        }

        return new Config(args.ToArray());
    }

    private static void Cleanup()
    {
        if (Directory.Exists(RootDir))
            Directory.Delete(RootDir, true);
    }
}
