using System.ComponentModel.DataAnnotations;
using AI_Workshop.Data;
using AI_Workshop.Models.Attendance;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages.Student.Attendance;

public sealed class QueryModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, AttendanceReviewService reviewService, InstitutionTimeService institutionTime) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public SessionView Session { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int sessionId) => await LoadAsync(sessionId) ? Page() : NotFound();
    public async Task<IActionResult> OnPostAsync(int sessionId, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(sessionId)) return NotFound();
        if (!ModelState.IsValid) return Page();
        try
        {
            await using var stream = Input.Evidence?.OpenReadStream();
            var id = await reviewService.SubmitAsync(sessionId, userManager.GetUserId(User)!, Input.RequestedStatus, Input.Explanation,
                Input.Evidence?.FileName, Input.Evidence?.ContentType, stream, Input.Evidence?.Length ?? 0, cancellationToken);
            TempData["StatusMessage"] = "Your attendance query was submitted.";
            return RedirectToPage("Queries", new { id });
        }
        catch (AttendanceReviewException exception) { ModelState.AddModelError(string.Empty, exception.Message); return Page(); }
    }
    private async Task<bool> LoadAsync(int sessionId)
    {
        var userId = userManager.GetUserId(User)!;
        var data = await db.LectureSessions.AsNoTracking().Where(value => value.Id == sessionId && value.Course.Enrollments.Any(link => link.StudentId == userId))
            .Select(value => new { value.Id, value.Course.Code, value.Topic, value.StartsAtUtc, Status = value.AttendanceRecords.Where(record => record.StudentId == userId).Select(record => (AttendanceStatus?)record.Status).FirstOrDefault() }).SingleOrDefaultAsync();
        if (data is null) return false;
        Session = new SessionView(data.Id, data.Code, data.Topic, institutionTime.ToLocal(data.StartsAtUtc), data.Status);
        return true;
    }
    public sealed class InputModel
    {
        [Display(Name = "Requested status")] public AttendanceStatus RequestedStatus { get; set; } = AttendanceStatus.Present;
        [Required, StringLength(2000, MinimumLength = 10)] public string Explanation { get; set; } = string.Empty;
        [Display(Name = "Supporting evidence")] public IFormFile? Evidence { get; set; }
    }
    public sealed record SessionView(int Id, string CourseCode, string Topic, DateTime StartsAt, AttendanceStatus? CurrentStatus);
}
