using System.ComponentModel.DataAnnotations;
using AI_Workshop.Data;
using AI_Workshop.Models.Academic;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages.Lecturer.Courses;

public sealed class DetailsModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, InstitutionTimeService institutionTime) : PageModel
{
    public CourseView Course { get; private set; } = null!;
    public IReadOnlyList<StudentRow> Students { get; private set; } = [];
    public IReadOnlyList<SessionRow> Sessions { get; private set; } = [];
    public string TimeZoneLabel => institutionTime.TimeZoneLabel;

    [BindProperty] public EnrolmentInput Enrolment { get; set; } = new();
    [BindProperty] public SessionInput Session { get; set; } = new();

    public sealed class EnrolmentInput
    {
        [Required, StringLength(150), Display(Name = "Student number or email")]
        public string Identifier { get; set; } = string.Empty;
    }

    public sealed class SessionInput : IValidatableObject
    {
        [Required, StringLength(140)] public string Topic { get; set; } = string.Empty;
        [StringLength(100)] public string? Venue { get; set; }
        [Required, Display(Name = "Starts at")] public DateTime StartsAt { get; set; } = DateTime.Now.Date.AddDays(1).AddHours(9);
        [Required, Display(Name = "Ends at")] public DateTime EndsAt { get; set; } = DateTime.Now.Date.AddDays(1).AddHours(10);

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndsAt <= StartsAt) yield return new ValidationResult("End time must be after the start time.", [nameof(EndsAt)]);
            if (EndsAt - StartsAt > TimeSpan.FromHours(8)) yield return new ValidationResult("A lecture session cannot exceed eight hours.", [nameof(EndsAt)]);
        }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostEnrolAsync(int id)
    {
        ModelState.Clear();
        if (!TryValidateModel(Enrolment, nameof(Enrolment)) || !await OwnsCourseAsync(id))
        {
            await LoadAsync(id);
            return Page();
        }

        var identifier = Enrolment.Identifier.Trim();
        var normalizedStudentNumber = identifier.ToUpperInvariant();
        var normalizedEmail = userManager.NormalizeEmail(identifier);
        var student = await db.Users.FirstOrDefaultAsync(user => user.StudentNumber == normalizedStudentNumber || user.NormalizedEmail == normalizedEmail);
        if (student is null || !await userManager.IsInRoleAsync(student, RoleNames.Student))
        {
            ModelState.AddModelError("Enrolment.Identifier", "No student account matches that student number or email.");
            await LoadAsync(id);
            return Page();
        }

        if (await db.Enrollments.AnyAsync(item => item.CourseId == id && item.StudentId == student.Id))
        {
            ModelState.AddModelError("Enrolment.Identifier", "This student is already enrolled in the course.");
            await LoadAsync(id);
            return Page();
        }

        db.Enrollments.Add(new Enrollment { CourseId = id, StudentId = student.Id });
        await db.SaveChangesAsync();
        TempData["StatusMessage"] = $"{student.DisplayName} was enrolled.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRemoveStudentAsync(int id, string studentId)
    {
        if (!await OwnsCourseAsync(id)) return NotFound();
        var enrolment = await db.Enrollments.FirstOrDefaultAsync(item => item.CourseId == id && item.StudentId == studentId);
        if (enrolment is null) return NotFound();
        db.Enrollments.Remove(enrolment);
        await db.SaveChangesAsync();
        TempData["StatusMessage"] = "The student was removed from this course.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAddSessionAsync(int id)
    {
        ModelState.Clear();
        if (!TryValidateModel(Session, nameof(Session)) || !await OwnsCourseAsync(id))
        {
            await LoadAsync(id);
            return Page();
        }

        db.LectureSessions.Add(new LectureSession { CourseId = id, Topic = Session.Topic.Trim(), Venue = Session.Venue?.Trim(), StartsAtUtc = institutionTime.ToUtc(Session.StartsAt), EndsAtUtc = institutionTime.ToUtc(Session.EndsAt) });
        await db.SaveChangesAsync();
        TempData["StatusMessage"] = "The lecture session was scheduled.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCancelSessionAsync(int id, int sessionId)
    {
        if (!await OwnsCourseAsync(id)) return NotFound();
        var session = await db.LectureSessions.FirstOrDefaultAsync(item => item.Id == sessionId && item.CourseId == id);
        if (session is null) return NotFound();
        session.Status = LectureSessionStatus.Cancelled;
        await db.SaveChangesAsync();
        TempData["StatusMessage"] = "The lecture session was cancelled.";
        return RedirectToPage(new { id });
    }

    private Task<bool> OwnsCourseAsync(int courseId)
    {
        var userId = userManager.GetUserId(User)!;
        return db.CourseLecturers.AnyAsync(item => item.CourseId == courseId && item.LecturerId == userId);
    }

    private async Task<bool> LoadAsync(int id)
    {
        var userId = userManager.GetUserId(User)!;
        var course = await db.Courses.AsNoTracking().Where(item => item.Id == id && item.Lecturers.Any(link => link.LecturerId == userId)).Select(item => new CourseView(item.Id, item.Code, item.Name, item.Description, item.AcademicYear, item.Semester, item.IsArchived)).FirstOrDefaultAsync();
        if (course is null) return false;
        Course = course;
        Students = await db.Enrollments.AsNoTracking().Where(item => item.CourseId == id).OrderBy(item => item.Student.DisplayName).Select(item => new StudentRow(item.StudentId, item.Student.DisplayName, item.Student.StudentNumber ?? "—", item.Student.Email ?? "—")).ToListAsync();
        Sessions = await db.LectureSessions.AsNoTracking().Where(item => item.CourseId == id).OrderBy(item => item.StartsAtUtc).Select(item => new SessionRow(item.Id, item.Topic, item.Venue, item.StartsAtUtc, item.EndsAtUtc, item.Status)).ToListAsync();
        return true;
    }

    public sealed record CourseView(int Id, string Code, string Name, string? Description, int AcademicYear, int Semester, bool IsArchived);
    public sealed record StudentRow(string Id, string Name, string StudentNumber, string Email);
    public sealed record SessionRow(int Id, string Topic, string? Venue, DateTime StartsAtUtc, DateTime EndsAtUtc, LectureSessionStatus Status)
    {
        public DateTime LocalStart(InstitutionTimeService time) => time.ToLocal(StartsAtUtc);
        public DateTime LocalEnd(InstitutionTimeService time) => time.ToLocal(EndsAtUtc);
    }
}
