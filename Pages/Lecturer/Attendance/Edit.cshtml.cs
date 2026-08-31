using System.ComponentModel.DataAnnotations;
using AI_Workshop.Data;
using AI_Workshop.Models.Attendance;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages.Lecturer.Attendance;

public sealed class EditModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, AttendanceReviewService reviewService, InstitutionTimeService institutionTime) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public EditView Attendance { get; private set; } = null!;
    public IReadOnlyList<AuditRow> Audit { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync(int sessionId, string studentId)
    {
        if (!await LoadAsync(sessionId, studentId)) return NotFound();
        Input.Status = Attendance.CurrentStatus ?? AttendanceStatus.Absent;
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(int sessionId, string studentId, CancellationToken token)
    {
        if (!await LoadAsync(sessionId, studentId)) return NotFound();
        if (!ModelState.IsValid) return Page();
        try
        {
            await reviewService.UpdateAttendanceAsync(sessionId, studentId, userManager.GetUserId(User)!, Input.Status, Input.Reason, token);
            TempData["StatusMessage"] = "Attendance was updated and added to the audit trail.";
            return RedirectToPage("Edit", new { sessionId, studentId });
        }
        catch (AttendanceReviewException exception) { ModelState.AddModelError(string.Empty, exception.Message); return Page(); }
    }
    private async Task<bool> LoadAsync(int sessionId, string studentId)
    {
        var lecturerId = userManager.GetUserId(User)!;
        var data = await db.LectureSessions.AsNoTracking().Where(session => session.Id == sessionId && session.Course.Lecturers.Any(link => link.LecturerId == lecturerId) && session.Course.Enrollments.Any(link => link.StudentId == studentId))
            .Select(session => new { session.Id, session.Course.Code, session.Topic, session.StartsAtUtc, Student = session.Course.Enrollments.Where(link => link.StudentId == studentId).Select(link => new { link.Student.DisplayName, link.Student.StudentNumber }).First(), Status = session.AttendanceRecords.Where(record => record.StudentId == studentId).Select(record => (AttendanceStatus?)record.Status).FirstOrDefault() }).SingleOrDefaultAsync();
        if (data is null) return false;
        Attendance = new EditView(data.Id, data.Code, data.Topic, institutionTime.ToLocal(data.StartsAtUtc), data.Student.DisplayName, data.Student.StudentNumber ?? "—", data.Status);
        var logs = await db.AttendanceChangeLogs.AsNoTracking().Where(log => log.LectureSessionId == sessionId && log.StudentId == studentId).OrderByDescending(log => log.ChangedAtUtc)
            .Select(log => new AuditRow(log.PreviousStatus, log.NewStatus, log.Reason, log.ChangedByLecturer.DisplayName, log.ChangedAtUtc)).ToListAsync();
        Audit = logs.Select(log => log with { ChangedAt = institutionTime.ToLocal(log.ChangedAt) }).ToList();
        return true;
    }
    public sealed class InputModel { public AttendanceStatus Status { get; set; } [Required, StringLength(1000, MinimumLength = 5)] public string Reason { get; set; } = string.Empty; }
    public sealed record EditView(int SessionId, string CourseCode, string Topic, DateTime StartsAt, string StudentName, string StudentNumber, AttendanceStatus? CurrentStatus);
    public sealed record AuditRow(AttendanceStatus? PreviousStatus, AttendanceStatus NewStatus, string Reason, string LecturerName, DateTime ChangedAt);
}
