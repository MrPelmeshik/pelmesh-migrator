namespace Migrator;

/// <summary>
/// Набор скриптов, привязанных к одной базе данных.
/// </summary>
public class DatabaseMigrations(string database, MigrationScript[] scripts)
{
    public string Database { get; } = database;
    public MigrationScript[] Scripts { get; } = scripts;
}
