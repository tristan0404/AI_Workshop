using System.ComponentModel.DataAnnotations;
using AI_Workshop.Models.Identity;

namespace AI_Workshop.Models.Academic;

public sealed class Course
{
    public int Id { get; set; }

    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Range(2020, 2100)]
    public int AcademicYear { get; set; } = DateTime.UtcNow.Year;

    [Range(1, 4)]
    public int Semester { get; set; } = 1;

    [StringLength(300)]
    public string? Description { get; set; }

    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<CourseLecturer> Lecturers { get; set; } = [];
    public ICollection<Enrollment> Enrollments { get; set; } = [];
    public ICollection<LectureSession> LectureSessions { get; set; } = [];
}

public sealed class CourseLecturer
{
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string LecturerId { get; set; } = string.Empty;
    public ApplicationUser Lecturer { get; set; } = null!;
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Enrollment
{
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser Student { get; set; } = null!;
    public DateTime EnrolledAtUtc { get; set; } = DateTime.UtcNow;
}
