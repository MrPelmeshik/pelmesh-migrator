namespace Migrator;

/// <summary>
/// Политика поведения при ошибке SQL.
/// </summary>
public enum OnErrorMode
{
    /// <summary>Ошибка прерывает прогон (default).</summary>
    Fail,

    /// <summary>Ошибка не останавливает следующие файлы.</summary>
    Continue
}
