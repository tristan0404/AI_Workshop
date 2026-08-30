using AI_Workshop.Data;
using Microsoft.AspNetCore.Identity;
using AI_Workshop.Models.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages.Lecturer.Courses;

public sealed class IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : PageModel
{
    public IReadOnlyList<CourseRow> Courses { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var userId = userManager.GetUserId(User)!;
        Courses = await db.CourseLecturers
            .Where(item => item.LecturerId == userId)
            .OrderBy(item => item.Course.IsArchived)
            .ThenBy(item => item.Course.Code)
            .Select(item => new CourseRow(
                item.Course.Id,
                item.Course.Code,
                item.Course.Name,
                item.Course.AcademicYear,
                item.Course.Semester,
                item.Course.Enrollments.Count,
                item.Course.LectureSessions.Count,
                item.Course.IsArchived))
            .ToListAsync();
    }

    public sealed record CourseRow(int Id, string Code, string Name, int AcademicYear, int Semester, int StudentCount, int SessionCount, bool IsArchived);
}
