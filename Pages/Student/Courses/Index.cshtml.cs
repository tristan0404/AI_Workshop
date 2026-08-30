using AI_Workshop.Data;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages.Student.Courses;

public sealed class IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, InstitutionTimeService institutionTime) : PageModel
{
    public IReadOnlyList<CourseRow> Courses { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var userId = userManager.GetUserId(User)!;
        var now = DateTime.UtcNow;
        var results = await db.Enrollments.AsNoTracking()
            .Where(item => item.StudentId == userId && !item.Course.IsArchived)
            .OrderBy(item => item.Course.Code)
            .Select(item => new { item.Course.Id, item.Course.Code, item.Course.Name, item.Course.AcademicYear, item.Course.Semester, Lecturer = item.Course.Lecturers.Select(link => link.Lecturer.DisplayName).FirstOrDefault(), NextSession = item.Course.LectureSessions.Where(session => session.StartsAtUtc >= now && session.Status == Models.Academic.LectureSessionStatus.Scheduled).OrderBy(session => session.StartsAtUtc).Select(session => (DateTime?)session.StartsAtUtc).FirstOrDefault() })
            .ToListAsync();
        Courses = results.Select(item => new CourseRow(item.Id, item.Code, item.Name, item.AcademicYear, item.Semester, item.Lecturer ?? "Lecturer TBC", item.NextSession is null ? null : institutionTime.ToLocal(item.NextSession.Value))).ToList();
    }

    public sealed record CourseRow(int Id, string Code, string Name, int AcademicYear, int Semester, string Lecturer, DateTime? NextSession);
}
