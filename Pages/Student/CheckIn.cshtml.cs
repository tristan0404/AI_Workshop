using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using AI_Workshop.Data;
using AI_Workshop.Models.Academic;
using AI_Workshop.Models.Attendance;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages.Student;

public sealed class CheckInModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    AttendanceTokenService tokenService,
    AttendanceService attendanceService,
    InstitutionTimeService institutionTime) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Token { get; set; }
    [BindProperty] public FallbackInput Input { get; set; } = new();
    public SessionView? Session { get; private set; }
    public CheckInResultType? ResultType { get; private set; }
    public string ResultMessage { get; private set; } = string.Empty;

    public sealed class FallbackInput
    {
        [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Enter the six-digit code shown by your lecturer.")]
        [Display(Name = "Fallback code")]
        public string Code { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Token)) return Page();
        if (!tokenService.TryReadQrToken(Token, out var sessionId))
        {
            SetResult(CheckInResultType.Expired);
            return Page();
        }
        if (!await LoadEnrolledSessionAsync(sessionId)) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostQrAsync()
    {
        ModelState.Clear();
        if (string.IsNullOrWhiteSpace(Token) || !tokenService.TryReadQrToken(Token, out var sessionId))
        {
            SetResult(CheckInResultType.Expired);
            return Page();
        }
        var result = await attendanceService.RecordAsync(sessionId, userManager.GetUserId(User)!, AttendanceSource.QrCode);
        await ApplyResultAsync(result);
        return Page();
    }

    public async Task<IActionResult> OnPostCodeAsync()
    {
        Token = null;
        if (!ModelState.IsValid) return Page();
        var studentId = userManager.GetUserId(User)!;
        var candidates = await db.LectureSessions.Include(item => item.Course).Where(item => item.AttendanceState == AttendanceWindowState.Open && item.AttendanceClosesAtUtc > DateTime.UtcNow && item.Course.Enrollments.Any(enrolment => enrolment.StudentId == studentId)).ToListAsync();
        var matchingSession = candidates.FirstOrDefault(session => CodesMatch(Input.Code, tokenService.TryReadFallbackCode(session.FallbackCodeProtected)));
        if (matchingSession is null)
        {
            ModelState.AddModelError("Input.Code", "That code is invalid or has expired.");
            return Page();
        }
        var result = await attendanceService.RecordAsync(matchingSession.Id, studentId, AttendanceSource.FallbackCode);
        await ApplyResultAsync(result);
        return Page();
    }

    private async Task ApplyResultAsync(CheckInResult result)
    {
        ResultType = result.Type;
        SetResult(result.Type);
        if (result.Session is not null) await LoadEnrolledSessionAsync(result.Session.Id);
    }

    private void SetResult(CheckInResultType type)
    {
        ResultType = type;
        ResultMessage = type switch
        {
            CheckInResultType.Recorded => "You’re checked in.",
            CheckInResultType.AlreadyRecorded => "You’re already checked in.",
            CheckInResultType.NotEnrolled => "You are not enrolled in this course.",
            CheckInResultType.Closed => "Attendance is closed for this lecture.",
            CheckInResultType.Expired => "This check-in link has expired. Scan the latest QR code.",
            _ => "This check-in could not be verified."
        };
    }

    private async Task<bool> LoadEnrolledSessionAsync(int sessionId)
    {
        var studentId = userManager.GetUserId(User)!;
        var result = await db.LectureSessions.AsNoTracking().Where(item => item.Id == sessionId && item.Course.Enrollments.Any(enrolment => enrolment.StudentId == studentId)).Select(item => new { item.Id, item.Topic, item.Venue, item.StartsAtUtc, item.EndsAtUtc, item.AttendanceState, item.AttendanceClosesAtUtc, item.Course.Code, CourseName = item.Course.Name }).FirstOrDefaultAsync();
        if (result is null) return false;
        Session = new SessionView(result.Id, result.Code, result.CourseName, result.Topic, result.Venue, institutionTime.ToLocal(result.StartsAtUtc), result.AttendanceState == AttendanceWindowState.Open && result.AttendanceClosesAtUtc > DateTime.UtcNow);
        return true;
    }

    private static bool CodesMatch(string supplied, string? expected)
    {
        if (expected is null || supplied.Length != expected.Length) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected));
    }

    public sealed record SessionView(int Id, string CourseCode, string CourseName, string Topic, string? Venue, DateTime StartsAt, bool IsOpen);
}
