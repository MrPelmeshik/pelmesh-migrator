using System.Collections;
using Migrator;

namespace Tests;

public class Extends_CheckMigrations_Tests
{
    private const string EmptySql = "-- empty sql";

    [Test, TestCaseSource(typeof(TestCases), nameof(TestCases.GetTestCases))]
    public void Test(
        IList<MigrationScript> migrationScripts,
        IList<MigrationHistory> migrationHistories,
        bool verifyHash,
        bool expectedResult)
    {
        Assert.That(
            Extends.CheckMigrations(migrationScripts, migrationHistories, verifyHash),
            Is.EqualTo(expectedResult));
    }

    private class TestCases
    {
        public static IEnumerable GetTestCases
        {
            get
            {
                yield return new TestCaseData(
                    new List<MigrationScript>(),
                    new List<MigrationHistory>(),
                    true,
                    true
                ).SetName("Проверка если нет скриптов миграции");
                yield return new TestCaseData(
                    new List<MigrationScript>
                    {
                        new MigrationScript(1, "TestFileName", "TestName", EmptySql),
                    },
                    new List<MigrationHistory>(),
                    true,
                    true
                ).SetName("Проверка если в истории нет скриптов, но есть в папке");
                yield return new TestCaseData(
                    new List<MigrationScript>
                    {
                        new MigrationScript(1, "TestFileName", "TestName", EmptySql),
                    },
                    new List<MigrationHistory>
                    {
                        new MigrationHistory(1, Extends.ComputeHash(EmptySql))
                    },
                    true,
                    true
                ).SetName("Проверка если в истории есть скрипт и есть в папке");
                yield return new TestCaseData(
                    new List<MigrationScript>
                    {
                        new MigrationScript(1, "1.a.sql", "a", EmptySql),
                        new MigrationScript(2, "2.b.sql", "b", EmptySql),
                    },
                    new List<MigrationHistory>
                    {
                        new MigrationHistory(1, Extends.ComputeHash(EmptySql))
                    },
                    true,
                    true
                ).SetName("Один скрипт ещё не выполнен");
                yield return new TestCaseData(
                    new List<MigrationScript>
                    {
                        new MigrationScript(1, "TestFileName", "TestName", EmptySql),
                    },
                    new List<MigrationHistory>
                    {
                        new MigrationHistory(1, Extends.ComputeHash(EmptySql)),
                        new MigrationHistory(2, Extends.ComputeHash(EmptySql))
                    },
                    true,
                    false
                ).SetName("В папке с миграциями не хватает файла");
                yield return new TestCaseData(
                    new List<MigrationScript>
                    {
                        new MigrationScript(1, "1.a.sql", "a", EmptySql),
                    },
                    new List<MigrationHistory>
                    {
                        new MigrationHistory(1, Extends.ComputeHash(EmptySql + "fake"))
                    },
                    true,
                    false
                ).SetName("Не совпадает хеш при verifyHash=true");
                yield return new TestCaseData(
                    new List<MigrationScript>
                    {
                        new MigrationScript(1, "1.a.sql", "a", EmptySql),
                    },
                    new List<MigrationHistory>
                    {
                        new MigrationHistory(1, Extends.ComputeHash(EmptySql + "fake"))
                    },
                    false,
                    true
                ).SetName("Расхождение хеша игнорируется при verifyHash=false");
            }
        }
    }
}
