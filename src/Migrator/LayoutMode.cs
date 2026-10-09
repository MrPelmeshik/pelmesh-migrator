namespace Migrator;

/// <summary>
/// Режим раскладки скриптов и выбора базы данных.
/// </summary>
public enum LayoutMode
{
    /// <summary>БД из имени подкаталога (default).</summary>
    Nested,

    /// <summary>Все скрипты в одну БД из --database.</summary>
    Flat,

    /// <summary>БД из сегмента имени файла.</summary>
    ByFilename
}
