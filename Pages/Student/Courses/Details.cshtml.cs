using AI_Workshop.Data;
using AI_Workshop.Models.Academic;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages.Student.Courses;

public sealed class DetailsModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, InstitutionTimeService institutionTime) : PageModel
{
    public CourseView Course { get; private set; } = null!;
    public IReadOnlyList<SessionRow> Sessions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = userManager.GetUserId(User)!;
        Course = (await db.Enrollments.AsNoTracking().Where(item => item.CourseId == id && item.StudentId == userId).Select(item => new CourseView(item.Course.Id, item.Course.Code, item.Course.Name, item.Course.Description, item.Course.AcademicYear, item.Course.Semester, item.Course.Lecturers.Select(link => link.Lecturer.DisplayName).FirstOrDefault() ?? "Lecturer TBC")).FirstOrDefaultAsync())!;
        if (Course is null) return NotFound();
        var sessions = await db.LectureSessions.AsNoTracking().Where(item => item.CourseId == id).OrderBy(item => item.StartsAtUtc).ToListAsync();
        Sessions = sessions.Select(item => new SessionRow(item.Id, item.Topic, item.Venue, institutionTime.ToLocal(item.StartsAtUtc), institutionTime.ToLocal(item.EndsAtUtc), item.Status)).ToList();
        return Page();
    }

    public sealed record CourseView(int Id, string Code, string Name, string? Description, int AcademicYear, int Semester, string Lecturer);
    public sealed record SessionRow(int Id, string Topic, string? Venue, DateTime StartsAt, DateTime EndsAt, LectureSessionStatus Status);
}
