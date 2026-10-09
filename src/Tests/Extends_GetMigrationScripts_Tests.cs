using System.Collections;
using Migrator;

namespace Tests;

public class Extends_GetMigrationScripts_Tests
{
    private const string MigrationDir = "./test_migration";
    private const string EmptySql = "-- empty sql";

    [Test, TestCaseSource(typeof(TestCases), nameof(TestCases.GetTestCases))]
    public void Test(
        Action initFiles,
        Action removeFiles,
        MigrationScript[] expectedMigrationScripts)
    {
        try
        {
            initFiles();

            var migrationScripts = Extends.GetMigrationScripts(MigrationDir);

            Assert.That(migrationScripts.Length, Is.EqualTo(expectedMigrationScripts.Length));

            Assert.That(expectedMigrationScripts.All(expected =>
                migrationScripts.Any(actual =>
                    actual.Order == expected.Order
                    && actual.Hash == expected.Hash
                    && actual.FileName == expected.FileName
                    && actual.Sql == expected.Sql
                    && actual.Name == expected.Name)));
        }
        finally
        {
            removeFiles();
        }
    }

    [Test]
    public void Unmatched_WithoutIgnore_Throws()
    {
        PrepareDir();
        try
        {
            File.WriteAllText(Path.Combine(MigrationDir, "readme.sql"), EmptySql);
            Assert.Throws<Exception>(() => Extends.GetMigrationScripts(MigrationDir));
        }
        finally
        {
            CleanupDir();
        }
    }

    [Test]
    public void Unmatched_WithIgnore_Skips()
    {
        PrepareDir();
        try
        {
            File.WriteAllText(Path.Combine(MigrationDir, "0001.ok.sql"), EmptySql);
            File.WriteAllText(Path.Combine(MigrationDir, "readme.sql"), EmptySql);
            var scripts = Extends.GetMigrationScripts(MigrationDir, LayoutMode.Nested, ignoreUnmatched: true);
            Assert.That(scripts.Length, Is.EqualTo(1));
            Assert.That(scripts[0].Order, Is.EqualTo(1));
        }
        finally
        {
            CleanupDir();
        }
    }

    [Test]
    public void DuplicateOrder_Throws()
    {
        PrepareDir();
        try
        {
            File.WriteAllText(Path.Combine(MigrationDir, "1.a.sql"), EmptySql);
            File.WriteAllText(Path.Combine(MigrationDir, "01.b.sql"), EmptySql);
            Assert.Throws<Exception>(() => Extends.GetMigrationScripts(MigrationDir));
        }
        finally
        {
            CleanupDir();
        }
    }

    [Test]
    public void NumericSort_ShortAndLongOrder()
    {
        PrepareDir();
        try
        {
            File.WriteAllText(Path.Combine(MigrationDir, "10001.app.late.sql"), EmptySql);
            File.WriteAllText(Path.Combine(MigrationDir, "1.app.init.sql"), EmptySql);
            var scripts = Extends.GetMigrationScripts(MigrationDir);
            Assert.That(scripts.Select(x => x.Order).ToArray(), Is.EqualTo(new[] { 1, 10001 }));
        }
        finally
        {
            CleanupDir();
        }
    }

    private static void PrepareDir()
    {
        CleanupDir();
        Directory.CreateDirectory(MigrationDir);
    }

    private static void CleanupDir()
    {
        if (Directory.Exists(MigrationDir))
            Directory.Delete(MigrationDir, true);
    }

    private class TestCases
    {
        public static IEnumerable GetTestCases
        {
            get
            {
                yield return new TestCaseData(
                        new Action(() =>
                        {
                            if (!Directory.Exists(MigrationDir))
                                Directory.CreateDirectory(MigrationDir);
                        }),
                        new Action(() =>
                        {
                            if (Directory.Exists(MigrationDir))
                                Directory.Delete(MigrationDir, true);
                        }),
                        Array.Empty<MigrationScript>())
                    .SetName("Тест пустой директории");
                yield return new TestCaseData(
                        new Action(() =>
                        {
                            if (!Directory.Exists(MigrationDir))
                                Directory.CreateDirectory(MigrationDir);
                            File.WriteAllText(Path.Combine(MigrationDir, "0001.test1.sql"), EmptySql);
                        }),
                        new Action(() =>
                        {
                            if (Directory.Exists(MigrationDir))
                                Directory.Delete(MigrationDir, true);
                        }),
                        new[]
                        {
                            new MigrationScript(1, "0001.test1.sql", "test1", EmptySql),
                        })
                    .SetName("Тест директории с одним файлом");
                yield return new TestCaseData(
                        new Action(() =>
                        {
                            if (!Directory.Exists(MigrationDir))
                                Directory.CreateDirectory(MigrationDir);
                            File.WriteAllText(Path.Combine(MigrationDir, "0001.test1.sql"), EmptySql);
                            File.WriteAllText(Path.Combine(MigrationDir, "0002.test2.sql"), EmptySql);
                        }),
                        new Action(() =>
                        {
                            if (Directory.Exists(MigrationDir))
                                Directory.Delete(MigrationDir, true);
                        }),
                        new[]
                        {
                            new MigrationScript(1, "0001.test1.sql", "test1", EmptySql),
                            new MigrationScript(2, "0002.test2.sql", "test2", EmptySql)
                        })
                    .SetName("Тест директории с несколькими файлами");
            }
        }
    }
}
