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
        int lastAppliedMigrationOrder,
        bool expectedResult)
    {
        Assert.That(expectedResult,
            Is.EqualTo(Extends.CheckMigrations(migrationScripts, migrationHistories, lastAppliedMigrationOrder)));
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
                    0,
                    /* expected */ true
                ).SetName("Проверка если нет скриптов миграции");
                yield return new TestCaseData(
                    new List<MigrationScript>
                    {
                        new MigrationScript(
                            1,
                            "TestFileName",
                            "TestName",
                            EmptySql),
                    },
                    new List<MigrationHistory>(),
                    0,
                    /* expected */ true
                ).SetName("Проверка если в истории нет скриптов, но есть в папке");
                yield return new TestCaseData(
                    new List<MigrationScript>
                    {
                        new MigrationScript(
                            1,
                            "TestFileName",
                            "TestName",
                            EmptySql),
                    },
                    new List<MigrationHistory>
                    {
                        new MigrationHistory(
                            1,
                            Extends.ComputeHash(EmptySql))
                    },
                    1,
                    /* expected */ true
                ).SetName("Проверка если в истории есть скрипт и есть в папке");
                yield return new TestCaseData(
                    new List<MigrationScript>
                    {
                        new MigrationScript(
                            1,
                            "TestFileName",
                            "TestName",
                            EmptySql),
                        new MigrationScript(
                            2,
                            "TestFileName",
                            "TestName",
                            EmptySql),
                    },
                    new List<MigrationHistory>
                    {
                        new MigrationHistory(
                            1,
                            Extends.ComputeHash(EmptySql))
                    },
                    1,
                    /* expected */ true
                ).SetName(
                    "Проверка если в истории есть скрипт и есть в папке. При этом один из скриптов еще не выполнен");
                yield return new TestCaseData(
                    new List<MigrationScript>
                    {
                        new MigrationScript(
                            1,
                            "TestFileName",
                            "TestName",
                            EmptySql),
                    },
                    new List<MigrationHistory>
                    {
                        new MigrationHistory(
                            1,
                            Extends.ComputeHash(EmptySql)),
                        new MigrationHistory(
                            2,
                            Extends.ComputeHash(EmptySql))
                    },
                    1,
                    /* expected */ false
                ).SetName("В папке с миграциями не хватает файла");
                yield return new TestCaseData(
                    new List<MigrationScript>
                    {
                        new MigrationScript(
                            1,
                            "TestFileName",
                            "TestName",
                            EmptySql),
                    },
                    new List<MigrationHistory>
                    {
                        new MigrationHistory(
                            1,
                            Extends.ComputeHash(EmptySql)),
                        new MigrationHistory(
                            2,
                            Extends.ComputeHash(EmptySql + "fake"))
                    },
                    1,
                    /* expected */ false
                ).SetName("В папке с миграциями не совпадает хеш");
            }
        }
    }
}