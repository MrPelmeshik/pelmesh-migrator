using System.Collections;
using Migrator;

namespace Tests;

public class Extends_MigrationRegex_Tests
{
    [Test, TestCaseSource(typeof(NestedFlatCases), nameof(NestedFlatCases.GetTestCases))]
    public void NestedOrFlat_Test(string text, MyMatch expectedMyMatch)
    {
        var actualMatch = Extends.MigrationRegex.Match(text);

        Assert.That(actualMatch.Success, Is.EqualTo(expectedMyMatch.Success));
        Assert.That(actualMatch.Groups["order"].Value, Is.EqualTo(expectedMyMatch.MatchOrder));
        Assert.That(actualMatch.Groups["desc"].Value, Is.EqualTo(expectedMyMatch.MatchDesc));
    }

    [Test, TestCaseSource(typeof(ByFilenameCases), nameof(ByFilenameCases.GetTestCases))]
    public void ByFilename_Test(string text, ByFilenameMatch expected)
    {
        var actualMatch = Extends.ByFilenameRegex.Match(text);

        Assert.That(actualMatch.Success, Is.EqualTo(expected.Success));
        Assert.That(actualMatch.Groups["order"].Value, Is.EqualTo(expected.Order));
        Assert.That(actualMatch.Groups["database"].Value, Is.EqualTo(expected.Database));
        Assert.That(actualMatch.Groups["desc"].Value, Is.EqualTo(expected.Desc));
    }

    private class NestedFlatCases
    {
        public static IEnumerable GetTestCases
        {
            get
            {
                yield return new TestCaseData("file", new MyMatch(false))
                    .SetName("Несовпадение по regex");
                yield return new TestCaseData("0001.test.sql", new MyMatch(true, "0001", "test"))
                    .SetName("Полное совпадение");
                yield return new TestCaseData("1.app.init.sql", new MyMatch(true, "1", "app.init"))
                    .SetName("Короткий номер \\d+");
                yield return new TestCaseData("10001.app.late.sql", new MyMatch(true, "10001", "app.late"))
                    .SetName("Длинный номер \\d+");
                yield return new TestCaseData("0010.add_users_table.sql",
                        new MyMatch(true, "0010", "add_users_table"))
                    .SetName("Двузначный номер и snake_case описание");
                yield return new TestCaseData("1234.Описание-на-русском.sql",
                        new MyMatch(true, "1234", "Описание-на-русском"))
                    .SetName("Четырехзначный номер и кириллическое описание");
                yield return new TestCaseData("0002.add-users-table.sql",
                        new MyMatch(true, "0002", "add-users-table"))
                    .SetName("Описание с дефисами");
                yield return new TestCaseData("0003.add.users.table.sql",
                        new MyMatch(true, "0003", "add.users.table"))
                    .SetName("Описание с точками");
                yield return new TestCaseData("0004.add users table.sql",
                        new MyMatch(true, "0004", "add users table"))
                    .SetName("Описание с пробелами");
                yield return new TestCaseData("0005.sql", new MyMatch(false))
                    .SetName("Только номер без описания");
                yield return new TestCaseData("0006..sql", new MyMatch(false))
                    .SetName("Номер с пустым описанием (две точки)");
                yield return new TestCaseData("0007add_table.sql", new MyMatch(false))
                    .SetName("Нет точки между номером и описанием");
                yield return new TestCaseData("abcd.add_table.sql", new MyMatch(false))
                    .SetName("Номер не является числом");
                yield return new TestCaseData("0008.add_table.SQL", new MyMatch(true, "0008", "add_table"))
                    .SetName("Расширение файла в верхнем регистре");
                yield return new TestCaseData("0009.add_table.sqll", new MyMatch(false))
                    .SetName("Расширение файла не .sql");
            }
        }
    }

    private class ByFilenameCases
    {
        public static IEnumerable GetTestCases
        {
            get
            {
                yield return new TestCaseData(
                        "001.dq.meta.sql",
                        new ByFilenameMatch(true, "001", "dq", "meta"))
                    .SetName("by-filename: db и rest");
                yield return new TestCaseData(
                        "1806.dq.meta.alter_views.sql",
                        new ByFilenameMatch(true, "1806", "dq", "meta.alter_views"))
                    .SetName("by-filename: многоточечный rest");
                yield return new TestCaseData(
                        "029.ois.schemas.sql",
                        new ByFilenameMatch(true, "029", "ois", "schemas"))
                    .SetName("by-filename: короткий order");
                yield return new TestCaseData(
                        "0001.init.sql",
                        new ByFilenameMatch(false))
                    .SetName("by-filename: только описание — невалидно");
                yield return new TestCaseData(
                        "readme.sql",
                        new ByFilenameMatch(false))
                    .SetName("by-filename: без номера");
            }
        }
    }

    public class MyMatch(bool success, string matchOrder = "", string matchDesc = "")
    {
        public bool Success { get; init; } = success;
        public string MatchOrder { get; init; } = matchOrder;
        public string MatchDesc { get; init; } = matchDesc;
    }

    public class ByFilenameMatch(bool success, string order = "", string database = "", string desc = "")
    {
        public bool Success { get; init; } = success;
        public string Order { get; init; } = order;
        public string Database { get; init; } = database;
        public string Desc { get; init; } = desc;
    }
}
