using System.ComponentModel.DataAnnotations;
using AI_Workshop.Models.Academic;

namespace AI_Workshop.Models.Attendance;

public enum AttendanceImportStatus { Previewed, Completed, Failed }
public enum AttendanceImportAction { Create, Update, NoChange }
public enum AttendanceImportStudentAction { AlreadyEnrolled, EnrollExisting, CreateAndEnroll }

public sealed class AttendanceImportBatch
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string LecturerId { get; set; } = string.Empty;
    [Required, StringLength(255)] public string FileName { get; set; } = string.Empty;
    public AttendanceImportStatus Status { get; set; } = AttendanceImportStatus.Previewed;
    public int SpreadsheetRowCount { get; set; }
    public int RecordCount { get; set; }
    public int ErrorCount { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAtUtc { get; set; }
    public ICollection<AttendanceImportItem> Items { get; set; } = [];
    public ICollection<AttendanceImportError> Errors { get; set; } = [];
}

public sealed class AttendanceImportItem
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public AttendanceImportBatch Batch { get; set; } = null!;
    public int SpreadsheetRow { get; set; }
    public string? StudentId { get; set; }
    [StringLength(40)] public string StudentNumber { get; set; } = string.Empty;
    [StringLength(200)] public string StudentName { get; set; } = string.Empty;
    public DateTime LectureDate { get; set; }
    public int? LectureSessionId { get; set; }
    public AttendanceStatus Status { get; set; }
    public AttendanceImportAction Action { get; set; }
    public AttendanceImportStudentAction StudentAction { get; set; }
}

public sealed class AttendanceImportError
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public AttendanceImportBatch Batch { get; set; } = null!;
    public int? SpreadsheetRow { get; set; }
    [StringLength(120)] public string? Column { get; set; }
    [Required, StringLength(500)] public string Message { get; set; } = string.Empty;
}
