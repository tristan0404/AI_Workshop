namespace AI_Workshop.Configuration;

public sealed class AttendanceOptions
{
    public const string SectionName = "Attendance";

    [System.ComponentModel.DataAnnotations.Range(1, 180)]
    public int WindowMinutes { get; set; } = 10;

    [System.ComponentModel.DataAnnotations.Range(0, 180)]
    public int LateAfterMinutes { get; set; } = 5;

    [System.ComponentModel.DataAnnotations.Range(10, 300)]
    public int QrTokenSeconds { get; set; } = 35;
}
