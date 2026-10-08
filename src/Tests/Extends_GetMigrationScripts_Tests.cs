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
            
            Assert.That(expectedMigrationScripts.Length,
                Is.EqualTo(migrationScripts.Length));

            Assert.IsTrue(expectedMigrationScripts
                .All(expectedMigrationScript => migrationScripts
                    .Any(migrationScript => migrationScript.Order == expectedMigrationScript.Order
                            && migrationScript.Hash == expectedMigrationScript.Hash
                            && migrationScript.FileName == expectedMigrationScript.FileName
                            && migrationScript.Sql == expectedMigrationScript.Sql
                            && migrationScript.Name == expectedMigrationScript.Name)));
        }
        finally
        {
            removeFiles();
        }
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
                            {
                                Directory.Delete(MigrationDir, true);
                            }
                        }),
                        Array.Empty<MigrationScript>())
                    .SetName("Тест пустой дирректории");
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
                            {
                                Directory.Delete(MigrationDir, true);
                            }
                        }),
                        new MigrationScript[]
                        {
                            new MigrationScript(1, "0001.test1.sql", "test1", EmptySql),
                        })
                    .SetName("Тест дирректории с одним файлом");
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
                            {
                                Directory.Delete(MigrationDir, true);
                            }
                        }),
                        new MigrationScript[]
                        {
                            new MigrationScript(1, "0001.test1.sql", "test1", EmptySql),
                            new MigrationScript(2, "0002.test2.sql", "test2", EmptySql)
                        })
                    .SetName("Тест дирректории с несколькими файлами");
            }
        }
    }
}