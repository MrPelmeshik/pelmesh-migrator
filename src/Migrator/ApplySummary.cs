namespace Migrator;

/// <summary>
/// Итоги прогона миграций для stdout и exit code.
/// </summary>
public class ApplySummary
{
    public int Databases { get; set; }
    public int Applied { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }

    /// <summary>
    /// Формирует строку summary для stdout.
    /// </summary>
    public override string ToString()
        => $"databases={Databases} applied={Applied} skipped={Skipped} errors={Errors}";
}
