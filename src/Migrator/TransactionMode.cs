namespace Migrator;

/// <summary>
/// Политика транзакций при применении миграций.
/// </summary>
public enum TransactionMode
{
    /// <summary>Одна транзакция на все pending-файлы одной БД.</summary>
    All,

    /// <summary>Транзакция на каждый файл.</summary>
    PerFile,

    /// <summary>Без обёртки транзакцией.</summary>
    None
}
