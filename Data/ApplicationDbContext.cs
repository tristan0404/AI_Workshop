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
    public DbSet<OfficeHour> OfficeHours => Set<OfficeHour>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AttendanceImportBatch> AttendanceImportBatches => Set<AttendanceImportBatch>();
    public DbSet<AttendanceImportItem> AttendanceImportItems => Set<AttendanceImportItem>();
    public DbSet<AttendanceImportError> AttendanceImportErrors => Set<AttendanceImportError>();
    public DbSet<AttendanceQuery> AttendanceQueries => Set<AttendanceQuery>();
    public DbSet<AttendanceChangeLog> AttendanceChangeLogs => Set<AttendanceChangeLog>();

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

        builder.Entity<OfficeHour>()
            .HasIndex(officeHour => new { officeHour.LecturerId, officeHour.DayOfWeek, officeHour.StartsAt });
        builder.Entity<OfficeHour>()
            .HasOne(officeHour => officeHour.Lecturer)
            .WithMany(lecturer => lecturer.OfficeHours)
            .HasForeignKey(officeHour => officeHour.LecturerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AttendanceRecord>()
            .HasIndex(record => new { record.LectureSessionId, record.StudentId })
            .IsUnique();
        builder.Entity<AttendanceRecord>()
            .HasOne(record => record.Student)
            .WithMany(user => user.AttendanceRecords)
            .HasForeignKey(record => record.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AttendanceImportBatch>()
            .HasIndex(batch => new { batch.LecturerId, batch.UploadedAtUtc });
        builder.Entity<AttendanceImportBatch>()
            .HasOne(batch => batch.Course)
            .WithMany()
            .HasForeignKey(batch => batch.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AttendanceImportItem>()
            .HasIndex(item => new { item.BatchId, item.StudentNumber, item.LectureDate })
            .IsUnique();
        builder.Entity<AttendanceImportItem>()
            .HasOne(item => item.Batch)
            .WithMany(batch => batch.Items)
            .HasForeignKey(item => item.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<AttendanceImportError>()
            .HasOne(error => error.Batch)
            .WithMany(batch => batch.Errors)
            .HasForeignKey(error => error.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AttendanceQuery>()
            .HasIndex(query => new { query.LectureSessionId, query.StudentId, query.Status });
        builder.Entity<AttendanceQuery>()
            .HasOne(query => query.Student)
            .WithMany()
            .HasForeignKey(query => query.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AttendanceQuery>()
            .HasOne(query => query.ReviewedByLecturer)
            .WithMany()
            .HasForeignKey(query => query.ReviewedByLecturerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AttendanceQuery>()
            .HasOne(query => query.AttendanceRecord)
            .WithMany(record => record.Queries)
            .HasForeignKey(query => query.AttendanceRecordId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<AttendanceChangeLog>()
            .HasIndex(log => new { log.AttendanceRecordId, log.ChangedAtUtc });
        builder.Entity<AttendanceChangeLog>()
            .HasOne(log => log.AttendanceRecord)
            .WithMany(record => record.ChangeLogs)
            .HasForeignKey(log => log.AttendanceRecordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AttendanceChangeLog>()
            .HasOne(log => log.ChangedByLecturer)
            .WithMany()
            .HasForeignKey(log => log.ChangedByLecturerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AttendanceChangeLog>()
            .HasOne(log => log.AttendanceQuery)
            .WithMany()
            .HasForeignKey(log => log.AttendanceQueryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
