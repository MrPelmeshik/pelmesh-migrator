using System.Collections;
using Migrator;

namespace Tests;

public class Extends_MigrationRegex_Tests
{
    [Test, TestCaseSource(typeof(TestCases), nameof(TestCases.GetTestCases))]
    public void Test(
        string text,
        MyMatch expectedMyMatch)
    {
        var actualMatch = Extends.MigrationRegex.Match(text);
        
        Assert.That(expectedMyMatch.Success,
            Is.EqualTo(actualMatch.Success));
        Assert.That(expectedMyMatch.MatchOrder,
            Is.EqualTo(actualMatch.Groups["order"].Value));
        Assert.That(expectedMyMatch.MatchDesc,
            Is.EqualTo(actualMatch.Groups["desc"].Value));
    }

    private class TestCases
    {
        public static IEnumerable GetTestCases
        {
            get
            {
                yield return new TestCaseData(
                        "file",
                        new MyMatch(false))
                    .SetName("Несовпадение по regex");
                yield return new TestCaseData(
                        "0001.test.sql",
                        new MyMatch(
                            true,
                            "0001",
                            "test"))
                    .SetName("Полное совпадение");
                yield return new TestCaseData(
                        "0010.add_users_table.sql",
                        new MyMatch(
                            true,
                            "0010",
                            "add_users_table"))
                    .SetName("Двузначный номер и snake_case описание");
                yield return new TestCaseData(
                        "1234.Описание-на-русском.sql",
                        new MyMatch(
                            true,
                            "1234",
                            "Описание-на-русском"))
                    .SetName("Четырехзначный номер и кириллическое описание");
                yield return new TestCaseData(
                        "0002.add-users-table.sql",
                        new MyMatch(
                            true,
                            "0002",
                            "add-users-table"))
                    .SetName("Описание с дефисами");
                yield return new TestCaseData(
                        "0003.add.users.table.sql",
                        new MyMatch(
                            true,
                            "0003",
                            "add.users.table"))
                    .SetName("Описание с точками");
                yield return new TestCaseData(
                        "0004.add users table.sql",
                        new MyMatch(
                            true,
                            "0004",
                            "add users table"))
                    .SetName("Описание с пробелами");
                yield return new TestCaseData(
                        "0005.sql",
                        new MyMatch(false))
                    .SetName("Только номер без описания");
                yield return new TestCaseData(
                        "0006..sql",
                        new MyMatch(false))
                    .SetName("Номер с пустым описанием (две точки)");
                yield return new TestCaseData(
                        "0007add_table.sql",
                        new MyMatch(false))
                    .SetName("Нет точки между номером и описанием");
                yield return new TestCaseData(
                        "abcd.add_table.sql",
                        new MyMatch(false))
                    .SetName("Номер не является числом");
                yield return new TestCaseData(
                        "0008.add_table.SQL",
                        new MyMatch(
                            true,
                            "0008",
                            "add_table"))
                    .SetName("Расширение файла в верхнем регистре");
                yield return new TestCaseData(
                        "0009.add_table.sqll",
                        new MyMatch(false))
                    .SetName("Расширение файла не .sql");
            }
        }
    }

    public class MyMatch(bool success, string matchOrder = "", string matchDesc = "")
    {
        public bool Success { get; init; } = success;
        public string MatchOrder { get; init; } = matchOrder;
        public string MatchDesc { get; init; } = matchDesc;
    }
}