using AI_Workshop.Models.Academic;
using AI_Workshop.Models.Identity;

namespace AI_Workshop.Models.Attendance;

public enum AttendanceStatus { Present, Late, Absent, Excused }
public enum AttendanceSource { QrCode, FallbackCode, Lecturer }

public sealed class AttendanceRecord
{
    public int Id { get; set; }
    public int LectureSessionId { get; set; }
    public LectureSession LectureSession { get; set; } = null!;
    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser Student { get; set; } = null!;
    public AttendanceStatus Status { get; set; }
    public AttendanceSource Source { get; set; }
    public DateTime CheckedInAtUtc { get; set; } = DateTime.UtcNow;
    public bool RequiresReview { get; set; }
}
