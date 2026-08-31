using System.ComponentModel.DataAnnotations;
using AI_Workshop.Data;
using AI_Workshop.Models.Attendance;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages.Lecturer.Queries;

public sealed class DetailsModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, AttendanceReviewService reviewService, InstitutionTimeService institutionTime) : PageModel
{
    [BindProperty, Required, StringLength(2000, MinimumLength = 5)] public string DecisionReason { get; set; } = string.Empty;
    public QueryView Query { get; private set; } = null!;
    public IReadOnlyList<AuditRow> Audit { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync(int id) => await LoadAsync(id) ? Page() : NotFound();
    public async Task<IActionResult> OnPostStartReviewAsync(int id, CancellationToken token) => await HandleAsync(id, async () => await reviewService.StartReviewAsync(id, userManager.GetUserId(User)!, token), "Query marked as under review.");
    public async Task<IActionResult> OnPostApproveAsync(int id, CancellationToken token) => await ResolveAsync(id, true, token);
    public async Task<IActionResult> OnPostRejectAsync(int id, CancellationToken token) => await ResolveAsync(id, false, token);
    public async Task<IActionResult> OnGetEvidenceAsync(int id)
    {
        var lecturerId = userManager.GetUserId(User)!;
        var evidence = await db.AttendanceQueries.AsNoTracking().Where(value => value.Id == id && value.LectureSession.Course.Lecturers.Any(link => link.LecturerId == lecturerId))
            .Select(value => new { value.EvidenceContent, value.EvidenceContentType, value.EvidenceFileName }).SingleOrDefaultAsync();
        return evidence?.EvidenceContent is null ? NotFound() : File(evidence.EvidenceContent, evidence.EvidenceContentType!, evidence.EvidenceFileName);
    }
    private async Task<IActionResult> ResolveAsync(int id, bool approve, CancellationToken token)
    {
        if (!ModelState.IsValid) { if (!await LoadAsync(id)) return NotFound(); return Page(); }
        return await HandleAsync(id, async () => await reviewService.ResolveAsync(id, userManager.GetUserId(User)!, approve, DecisionReason, token), approve ? "The query was approved and attendance updated." : "The query was rejected.");
    }
    private async Task<IActionResult> HandleAsync(int id, Func<Task> action, string message)
    {
        try { await action(); TempData["StatusMessage"] = message; return RedirectToPage(new { id }); }
        catch (AttendanceReviewException exception) { ModelState.AddModelError(string.Empty, exception.Message); if (!await LoadAsync(id)) return NotFound(); return Page(); }
    }
    private async Task<bool> LoadAsync(int id)
    {
        var lecturerId = userManager.GetUserId(User)!;
        var value = await db.AttendanceQueries.AsNoTracking().Where(item => item.Id == id && item.LectureSession.Course.Lecturers.Any(link => link.LecturerId == lecturerId))
            .Select(item => new { item.Id, item.StudentId, item.LectureSessionId, item.Student.DisplayName, item.Student.StudentNumber, item.LectureSession.Course.Code, item.LectureSession.Topic, item.LectureSession.StartsAtUtc, CurrentStatus = item.AttendanceRecord == null ? (AttendanceStatus?)null : item.AttendanceRecord.Status, item.RequestedStatus, item.Explanation, item.Status, item.LecturerResponse, item.EvidenceFileName, item.SubmittedAtUtc, item.ReviewedAtUtc }).SingleOrDefaultAsync();
        if (value is null) return false;
        Query = new QueryView(value.Id, value.StudentId, value.DisplayName, value.StudentNumber ?? "—", value.Code, value.Topic, institutionTime.ToLocal(value.StartsAtUtc), value.CurrentStatus, value.RequestedStatus, value.Explanation, value.Status, value.LecturerResponse, value.EvidenceFileName, institutionTime.ToLocal(value.SubmittedAtUtc), value.ReviewedAtUtc is null ? null : institutionTime.ToLocal(value.ReviewedAtUtc.Value));
        Audit = await db.AttendanceChangeLogs.AsNoTracking().Where(log => log.LectureSessionId == value.LectureSessionId && log.StudentId == value.StudentId)
            .OrderByDescending(log => log.ChangedAtUtc).Select(log => new AuditRow(log.PreviousStatus, log.NewStatus, log.Reason, log.ChangedByLecturer.DisplayName, log.ChangedAtUtc)).ToListAsync();
        Audit = Audit.Select(log => log with { ChangedAt = institutionTime.ToLocal(log.ChangedAt) }).ToList();
        DecisionReason = value.LecturerResponse ?? DecisionReason;
        return true;
    }
    public sealed record QueryView(int Id, string StudentId, string StudentName, string StudentNumber, string CourseCode, string Topic, DateTime LectureDate, AttendanceStatus? CurrentStatus, AttendanceStatus RequestedStatus, string Explanation, AttendanceQueryStatus Status, string? LecturerResponse, string? EvidenceFileName, DateTime SubmittedAt, DateTime? ReviewedAt);
    public sealed record AuditRow(AttendanceStatus? PreviousStatus, AttendanceStatus NewStatus, string Reason, string LecturerName, DateTime ChangedAt);
}
