namespace AI_Workshop.Configuration;

public sealed class AttendanceImportOptions
{
    public const string SectionName = "AttendanceImport";

    [System.ComponentModel.DataAnnotations.Range(100_000, 20_000_000)]
    public int MaximumFileBytes { get; set; } = 2_000_000;

    [System.ComponentModel.DataAnnotations.Range(1, 50_000)]
    public int MaximumRows { get; set; } = 5_000;

    [System.ComponentModel.DataAnnotations.Range(0, 23)]
    public int HistoricalSessionStartHour { get; set; } = 9;

    [System.ComponentModel.DataAnnotations.Range(1, 480)]
    public int HistoricalSessionDurationMinutes { get; set; } = 60;
}
