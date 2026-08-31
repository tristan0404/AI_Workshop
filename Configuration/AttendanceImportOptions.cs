namespace AI_Workshop.Configuration;

public sealed class AttendanceImportOptions
{
    public const string SectionName = "AttendanceImport";
    public int MaximumFileBytes { get; set; } = 2_000_000;
    public int MaximumRows { get; set; } = 5_000;
    public int HistoricalSessionStartHour { get; set; } = 9;
    public int HistoricalSessionDurationMinutes { get; set; } = 60;
}
