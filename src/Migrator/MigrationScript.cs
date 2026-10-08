using System.Security.Cryptography;
using System.Text;

namespace Migrator;

public class MigrationScript(int order, string fileName, string name, string sql)
{
    public int Order { get; init; } = order > 0
        ? order
        : throw new Exception($"Неверный номер миграции: {order}. Должен быть больше нуля.");

    public string FileName { get; init; } = !string.IsNullOrEmpty(fileName)
        ? fileName
        : throw new Exception($"Неверное имя миграции: {fileName}. Должно быть не пустое.");

    public string Name { get; init; } = !string.IsNullOrEmpty(name)
        ? name
        : throw new Exception($"Неверное описание миграции: {name}. Должно быть не пустое.");

    public string Sql { get; init; } = !string.IsNullOrEmpty(sql)
        ? sql
        : throw new Exception($"Неверный текст миграции: {sql}. Должен быть не пустой.");

    public string Hash { get; init; } = Extends.ComputeHash(sql);
}