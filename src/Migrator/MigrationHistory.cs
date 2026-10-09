namespace Migrator;

/// <summary>
/// Запись из таблицы истории миграций.
/// </summary>
public class MigrationHistory
{
    public int Order { get; init; }
    public string Hash { get; init; }
    public string FileName { get; init; }
    public string Status { get; init; }

    public MigrationHistory(int order, string hash, string fileName = "", string status = "applied")
    {
        Order = order;
        Hash = hash;
        FileName = fileName;
        Status = status;
    }
}
