using Migrator;

namespace Tests;

public class HistorySchema_Tests
{
    [Test]
    public void UpgradeSql_ContainsNewColumns()
    {
        Assert.That(HistorySchema.UpgradeColumnsSql, Does.Contain("status"));
        Assert.That(HistorySchema.UpgradeColumnsSql, Does.Contain("applied_at"));
        Assert.That(HistorySchema.UpgradeColumnsSql, Does.Contain("error_text"));
        Assert.That(HistorySchema.UpgradeColumnsSql, Does.Contain("add column if not exists"));
    }

    [Test]
    public void UpgradeConstraints_ContainUniques()
    {
        Assert.That(HistorySchema.UpgradeConstraintsSql, Does.Contain("migration_history_order_number_key"));
        Assert.That(HistorySchema.UpgradeConstraintsSql, Does.Contain("migration_history_file_name_key"));
        Assert.That(HistorySchema.UpgradeConstraintsSql, Does.Contain("unique (order_number)"));
        Assert.That(HistorySchema.UpgradeConstraintsSql, Does.Contain("unique (file_name)"));
    }

    [Test]
    public void CreateTable_HasBaseColumns()
    {
        Assert.That(HistorySchema.CreateTableSql, Does.Contain("order_number"));
        Assert.That(HistorySchema.CreateTableSql, Does.Contain("file_name"));
        Assert.That(HistorySchema.CreateTableSql, Does.Contain("hash"));
    }
}
