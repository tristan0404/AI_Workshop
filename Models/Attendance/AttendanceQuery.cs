using System.ComponentModel.DataAnnotations;
using AI_Workshop.Models.Academic;
using AI_Workshop.Models.Identity;

namespace AI_Workshop.Models.Attendance;

public enum AttendanceQueryStatus { Submitted, UnderReview, Approved, Rejected }

public sealed class AttendanceQuery
{
    public int Id { get; set; }
    public int LectureSessionId { get; set; }
    public LectureSession LectureSession { get; set; } = null!;
    public int? AttendanceRecordId { get; set; }
    public AttendanceRecord? AttendanceRecord { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser Student { get; set; } = null!;
    public AttendanceStatus RequestedStatus { get; set; }
    [Required, StringLength(2000)] public string Explanation { get; set; } = string.Empty;
    public AttendanceQueryStatus Status { get; set; } = AttendanceQueryStatus.Submitted;
    [StringLength(2000)] public string? LecturerResponse { get; set; }
    public string? ReviewedByLecturerId { get; set; }
    public ApplicationUser? ReviewedByLecturer { get; set; }
    [StringLength(255)] public string? EvidenceFileName { get; set; }
    [StringLength(100)] public string? EvidenceContentType { get; set; }
    public byte[]? EvidenceContent { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
}

public sealed class AttendanceChangeLog
{
    public int Id { get; set; }
    public int LectureSessionId { get; set; }
    public LectureSession LectureSession { get; set; } = null!;
    public int AttendanceRecordId { get; set; }
    public AttendanceRecord AttendanceRecord { get; set; } = null!;
    public int? AttendanceQueryId { get; set; }
    public AttendanceQuery? AttendanceQuery { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string ChangedByLecturerId { get; set; } = string.Empty;
    public ApplicationUser ChangedByLecturer { get; set; } = null!;
    public AttendanceStatus? PreviousStatus { get; set; }
    public AttendanceStatus NewStatus { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
