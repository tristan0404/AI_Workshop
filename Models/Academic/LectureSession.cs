using System.ComponentModel.DataAnnotations;

namespace AI_Workshop.Models.Academic;

public enum LectureSessionStatus
{
    Scheduled,
    Cancelled,
    Completed
}

public enum AttendanceWindowState { Closed, Open }

public sealed class LectureSession
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    [Required, StringLength(140)]
    public string Topic { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Venue { get; set; }

    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public LectureSessionStatus Status { get; set; } = LectureSessionStatus.Scheduled;
    public AttendanceWindowState AttendanceState { get; set; } = AttendanceWindowState.Closed;
    public DateTime? AttendanceOpenedAtUtc { get; set; }
    public DateTime? AttendanceClosesAtUtc { get; set; }
    public string? FallbackCodeProtected { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Models.Attendance.AttendanceRecord> AttendanceRecords { get; set; } = [];
}
