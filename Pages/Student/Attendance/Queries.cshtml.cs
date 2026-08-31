using AI_Workshop.Data;
using AI_Workshop.Models.Attendance;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages.Student.Attendance;

public sealed class QueriesModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, InstitutionTimeService institutionTime) : PageModel
{
    public IReadOnlyList<QueryRow> Queries { get; private set; } = [];
    public async Task OnGetAsync()
    {
        var userId = userManager.GetUserId(User)!;
        var data = await db.AttendanceQueries.AsNoTracking().Where(value => value.StudentId == userId).OrderByDescending(value => value.SubmittedAtUtc)
            .Select(value => new { value.Id, value.LectureSession.Course.Code, value.LectureSession.Topic, value.LectureSession.StartsAtUtc, value.RequestedStatus, value.Explanation, value.Status, value.LecturerResponse, value.SubmittedAtUtc, value.ReviewedAtUtc }).ToListAsync();
        Queries = data.Select(value => new QueryRow(value.Id, value.Code, value.Topic, institutionTime.ToLocal(value.StartsAtUtc), value.RequestedStatus, value.Explanation, value.Status, value.LecturerResponse, institutionTime.ToLocal(value.SubmittedAtUtc), value.ReviewedAtUtc is null ? null : institutionTime.ToLocal(value.ReviewedAtUtc.Value))).ToList();
    }
    public sealed record QueryRow(int Id, string CourseCode, string Topic, DateTime LectureDate, AttendanceStatus RequestedStatus, string Explanation, AttendanceQueryStatus Status, string? LecturerResponse, DateTime SubmittedAt, DateTime? ReviewedAt);
}
