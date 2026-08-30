using AI_Workshop.Data;
using AI_Workshop.Models.Attendance;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages.Student.Attendance;

public sealed class IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, InstitutionTimeService institutionTime) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? CourseId { get; set; }
    public IReadOnlyList<AttendanceRow> Records { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Courses { get; private set; } = [];
    public int PresentCount => Records.Count(item => item.Status == AttendanceStatus.Present);
    public int LateCount => Records.Count(item => item.Status == AttendanceStatus.Late);

    public async Task OnGetAsync()
    {
        var userId = userManager.GetUserId(User)!;
        Courses = await db.Enrollments.Where(item => item.StudentId == userId).OrderBy(item => item.Course.Code).Select(item => new SelectListItem(item.Course.Code, item.CourseId.ToString())).ToListAsync();
        var query = db.AttendanceRecords.AsNoTracking().Where(item => item.StudentId == userId);
        if (CourseId is not null) query = query.Where(item => item.LectureSession.CourseId == CourseId);
        var data = await query.OrderByDescending(item => item.LectureSession.StartsAtUtc).Select(item => new { item.Id, item.LectureSession.Course.Code, item.LectureSession.Topic, item.LectureSession.StartsAtUtc, item.Status, item.Source, item.CheckedInAtUtc }).ToListAsync();
        Records = data.Select(item => new AttendanceRow(item.Id, item.Code, item.Topic, institutionTime.ToLocal(item.StartsAtUtc), item.Status, item.Source, institutionTime.ToLocal(item.CheckedInAtUtc))).ToList();
    }
    public sealed record AttendanceRow(int Id, string CourseCode, string Topic, DateTime LectureDate, AttendanceStatus Status, AttendanceSource Source, DateTime CheckedInAt);
}
