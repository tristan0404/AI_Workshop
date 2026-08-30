namespace AI_Workshop.Configuration;

public sealed class AttendanceOptions
{
    public const string SectionName = "Attendance";
    public int WindowMinutes { get; set; } = 10;
    public int LateAfterMinutes { get; set; } = 5;
    public int QrTokenSeconds { get; set; } = 35;
}
