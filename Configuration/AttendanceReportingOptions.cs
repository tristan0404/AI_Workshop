namespace AI_Workshop.Configuration;

public sealed class AttendanceReportingOptions
{
    public const string SectionName = "AttendanceReporting";

    [System.ComponentModel.DataAnnotations.Range(0, 100)]
    public decimal AtRiskPercentage { get; set; } = 75m;
}
