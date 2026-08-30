using AI_Workshop.Models.Identity;
using AI_Workshop.Models.Academic;
using AI_Workshop.Models.Attendance;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseLecturer> CourseLecturers => Set<CourseLecturer>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<LectureSession> LectureSessions => Set<LectureSession>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>()
            .HasIndex(user => user.StudentNumber)
            .IsUnique()
            .HasFilter("StudentNumber IS NOT NULL");

        builder.Entity<Course>()
            .HasIndex(course => new { course.Code, course.AcademicYear, course.Semester })
            .IsUnique();

        builder.Entity<CourseLecturer>().HasKey(item => new { item.CourseId, item.LecturerId });
        builder.Entity<CourseLecturer>()
            .HasOne(item => item.Lecturer)
            .WithMany(user => user.TeachingAssignments)
            .HasForeignKey(item => item.LecturerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Enrollment>().HasKey(item => new { item.CourseId, item.StudentId });
        builder.Entity<Enrollment>()
            .HasOne(item => item.Student)
            .WithMany(user => user.Enrollments)
            .HasForeignKey(item => item.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<LectureSession>()
            .HasIndex(session => new { session.CourseId, session.StartsAtUtc });

        builder.Entity<AttendanceRecord>()
            .HasIndex(record => new { record.LectureSessionId, record.StudentId })
            .IsUnique();
        builder.Entity<AttendanceRecord>()
            .HasOne(record => record.Student)
            .WithMany(user => user.AttendanceRecords)
            .HasForeignKey(record => record.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
