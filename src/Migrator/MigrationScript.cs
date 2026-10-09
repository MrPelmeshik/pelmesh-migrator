namespace Migrator;

/// <summary>
/// Скрипт миграции, разобранный из имени файла.
/// </summary>
public class MigrationScript
{
    public int Order { get; init; }
    public string FileName { get; init; }
    public string Name { get; init; }
    public string Sql { get; init; }
    public string Hash { get; init; }

    /// <summary>
    /// Имя БД из имени файла (только для layout by-filename).
    /// </summary>
    public string? Database { get; init; }

    public MigrationScript(int order, string fileName, string name, string sql, string? database = null)
    {
        Order = order > 0
            ? order
            : throw new Exception($"Неверный номер миграции: {order}. Должен быть больше нуля.");

        FileName = !string.IsNullOrEmpty(fileName)
            ? fileName
            : throw new Exception($"Неверное имя миграции: {fileName}. Должно быть не пустое.");

        Name = !string.IsNullOrEmpty(name)
            ? name
            : throw new Exception($"Неверное описание миграции: {name}. Должно быть не пустое.");

        Sql = !string.IsNullOrEmpty(sql)
            ? sql
            : throw new Exception($"Неверный текст миграции: {sql}. Должен быть не пустой.");

        Hash = Extends.ComputeHash(sql);
        Database = database;
    }
}
