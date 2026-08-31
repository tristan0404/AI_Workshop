using AI_Workshop.Data;
using AI_Workshop.Models.Attendance;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages.Lecturer.Queries;

public sealed class IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, InstitutionTimeService institutionTime) : PageModel
{
    [BindProperty(SupportsGet = true)] public AttendanceQueryStatus? Status { get; set; }
    public IReadOnlyList<QueryRow> Queries { get; private set; } = [];
    public int OpenCount { get; private set; }
    public async Task OnGetAsync()
    {
        var lecturerId = userManager.GetUserId(User)!;
        var owned = db.AttendanceQueries.AsNoTracking().Where(value => value.LectureSession.Course.Lecturers.Any(link => link.LecturerId == lecturerId));
        OpenCount = await owned.CountAsync(value => value.Status == AttendanceQueryStatus.Submitted || value.Status == AttendanceQueryStatus.UnderReview);
        if (Status is not null) owned = owned.Where(value => value.Status == Status);
        var data = await owned.OrderBy(value => value.Status == AttendanceQueryStatus.Submitted ? 0 : value.Status == AttendanceQueryStatus.UnderReview ? 1 : 2).ThenByDescending(value => value.SubmittedAtUtc)
            .Select(value => new { value.Id, value.Student.DisplayName, value.Student.StudentNumber, value.LectureSession.Course.Code, value.LectureSession.Topic, value.LectureSession.StartsAtUtc, value.RequestedStatus, value.Status, value.SubmittedAtUtc }).ToListAsync();
        Queries = data.Select(value => new QueryRow(value.Id, value.DisplayName, value.StudentNumber ?? "—", value.Code, value.Topic, institutionTime.ToLocal(value.StartsAtUtc), value.RequestedStatus, value.Status, institutionTime.ToLocal(value.SubmittedAtUtc))).ToList();
    }
    public sealed record QueryRow(int Id, string StudentName, string StudentNumber, string CourseCode, string Topic, DateTime LectureDate, AttendanceStatus RequestedStatus, AttendanceQueryStatus Status, DateTime SubmittedAt);
}
