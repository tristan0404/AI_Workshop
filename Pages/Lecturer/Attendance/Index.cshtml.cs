using AI_Workshop.Data;
using AI_Workshop.Models.Academic;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages.Lecturer.Attendance;

public sealed class IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, InstitutionTimeService institutionTime) : PageModel
{
    public IReadOnlyList<SessionRow> Sessions { get; private set; } = [];
    public async Task OnGetAsync()
    {
        var userId = userManager.GetUserId(User)!;
        var data = await db.LectureSessions.AsNoTracking().Where(item => item.Course.Lecturers.Any(link => link.LecturerId == userId)).OrderByDescending(item => item.StartsAtUtc).Select(item => new { item.Id, item.Course.Code, item.Topic, item.Venue, item.StartsAtUtc, item.AttendanceState, item.AttendanceClosesAtUtc, Enrolled = item.Course.Enrollments.Count, CheckedIn = item.AttendanceRecords.Count }).ToListAsync();
        Sessions = data.Select(item => new SessionRow(item.Id, item.Code, item.Topic, item.Venue, institutionTime.ToLocal(item.StartsAtUtc), item.AttendanceState == AttendanceWindowState.Open && item.AttendanceClosesAtUtc > DateTime.UtcNow, item.Enrolled, item.CheckedIn)).ToList();
    }
    public sealed record SessionRow(int Id, string CourseCode, string Topic, string? Venue, DateTime StartsAt, bool IsLive, int Enrolled, int CheckedIn);
}
