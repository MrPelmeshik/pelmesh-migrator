namespace Migrator;

public class MigrationHistory(int order, string hash)
{
    public int Order { get; init; } = order;
    public string Hash { get; init; } = hash;
}