using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using AI_Workshop.Models.Academic;
using AI_Workshop.Models.Attendance;

namespace AI_Workshop.Models.Identity;

public sealed class ApplicationUser : IdentityUser
{
    [PersonalData]
    [StringLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    [PersonalData]
    [StringLength(30)]
    public string? StudentNumber { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<CourseLecturer> TeachingAssignments { get; set; } = [];
    public ICollection<Enrollment> Enrollments { get; set; } = [];
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = [];
}
