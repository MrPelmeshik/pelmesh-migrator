using System.Text;

namespace Migrator;

/// <summary>
/// Разбивает SQL-скрипт на statements с учётом кавычек, комментариев и dollar-quoting.
/// </summary>
public static class SqlStatementSplitter
{
    /// <summary>
    /// Разбивает текст по `;` вне строковых литералов / dollar-quote / комментариев.
    /// </summary>
    public static IReadOnlyList<string> Split(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return Array.Empty<string>();

        var result = new List<string>();
        var current = new StringBuilder();
        var i = 0;
        string? dollarTag = null;
        var inSingle = false;
        var inDouble = false;
        var inLineComment = false;
        var inBlockComment = false;

        while (i < sql.Length)
        {
            var c = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (inLineComment)
            {
                current.Append(c);
                if (c is '\n' or '\r')
                    inLineComment = false;
                i++;
                continue;
            }

            if (inBlockComment)
            {
                current.Append(c);
                if (c == '*' && next == '/')
                {
                    current.Append(next);
                    i += 2;
                    inBlockComment = false;
                    continue;
                }

                i++;
                continue;
            }

            if (dollarTag != null)
            {
                if (StartsWith(sql, i, dollarTag))
                {
                    current.Append(dollarTag);
                    i += dollarTag.Length;
                    dollarTag = null;
                    continue;
                }

                current.Append(c);
                i++;
                continue;
            }

            if (inSingle)
            {
                current.Append(c);
                if (c == '\'')
                {
                    // '' — экранированная кавычка
                    if (next == '\'')
                    {
                        current.Append(next);
                        i += 2;
                        continue;
                    }

                    inSingle = false;
                }

                i++;
                continue;
            }

            if (inDouble)
            {
                current.Append(c);
                if (c == '"')
                {
                    if (next == '"')
                    {
                        current.Append(next);
                        i += 2;
                        continue;
                    }

                    inDouble = false;
                }

                i++;
                continue;
            }

            // Вне литералов
            if (c == '-' && next == '-')
            {
                current.Append(c);
                current.Append(next);
                i += 2;
                inLineComment = true;
                continue;
            }

            if (c == '/' && next == '*')
            {
                current.Append(c);
                current.Append(next);
                i += 2;
                inBlockComment = true;
                continue;
            }

            if (c == '\'')
            {
                current.Append(c);
                inSingle = true;
                i++;
                continue;
            }

            if (c == '"')
            {
                current.Append(c);
                inDouble = true;
                i++;
                continue;
            }

            if (c == '$')
            {
                var tag = ReadDollarTag(sql, i);
                if (tag != null)
                {
                    current.Append(tag);
                    i += tag.Length;
                    dollarTag = tag;
                    continue;
                }
            }

            if (c == ';')
            {
                var statement = current.ToString().Trim();
                if (statement.Length > 0)
                    result.Add(statement);
                current.Clear();
                i++;
                continue;
            }

            current.Append(c);
            i++;
        }

        var tail = current.ToString().Trim();
        if (tail.Length > 0)
            result.Add(tail);

        return result;
    }

    /// <summary>
    /// Читает dollar-tag вида $ или $tag$ начиная с позиции i; null если это не тег.
    /// </summary>
    private static string? ReadDollarTag(string sql, int i)
    {
        if (sql[i] != '$')
            return null;

        var j = i + 1;
        while (j < sql.Length)
        {
            var c = sql[j];
            if (c == '$')
                return sql[i..(j + 1)];

            // В теге: буквы, цифры, подчёркивание (упрощённо как PostgreSQL identifier)
            if (!(char.IsLetterOrDigit(c) || c == '_'))
                return null;

            j++;
        }

        return null;
    }

    private static bool StartsWith(string sql, int index, string value)
    {
        if (index + value.Length > sql.Length)
            return false;

        return string.CompareOrdinal(sql, index, value, 0, value.Length) == 0;
    }
}
