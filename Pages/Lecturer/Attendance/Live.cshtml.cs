using System.Security.Cryptography;
using System.Text;
using AI_Workshop.Configuration;
using AI_Workshop.Data;
using AI_Workshop.Models.Academic;
using AI_Workshop.Models.Attendance;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QRCoder;

namespace AI_Workshop.Pages.Lecturer.Attendance;

public sealed class LiveModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    AttendanceTokenService tokenService,
    AttendanceService attendanceService,
    InstitutionTimeService institutionTime,
    IOptions<AttendanceOptions> attendanceOptions) : PageModel
{
    public SessionView Session { get; private set; } = null!;
    public IReadOnlyList<RosterRow> Roster { get; private set; } = [];
    public string? FallbackCode { get; private set; }
    public int QrRefreshSeconds => Math.Max(10, attendanceOptions.Value.QrTokenSeconds - 5);

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostOpenAsync(int id)
    {
        var session = await FindOwnedSessionAsync(id);
        if (session is null) return NotFound();
        if (session.Status == LectureSessionStatus.Cancelled)
        {
            TempData["ErrorMessage"] = "A cancelled lecture cannot accept attendance.";
            return RedirectToPage(new { id });
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        session.AttendanceState = AttendanceWindowState.Open;
        session.AttendanceOpenedAtUtc = DateTime.UtcNow;
        session.AttendanceClosesAtUtc = DateTime.UtcNow.AddMinutes(attendanceOptions.Value.WindowMinutes);
        session.FallbackCodeProtected = tokenService.ProtectFallbackCode(code);
        await db.SaveChangesAsync();
        TempData["StatusMessage"] = $"Attendance is open for {attendanceOptions.Value.WindowMinutes} minutes.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCloseAsync(int id)
    {
        var session = await FindOwnedSessionAsync(id);
        if (session is null) return NotFound();
        session.AttendanceState = AttendanceWindowState.Closed;
        session.AttendanceClosesAtUtc = DateTime.UtcNow;
        session.FallbackCodeProtected = null;
        await db.SaveChangesAsync();
        TempData["StatusMessage"] = "Attendance was closed.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostMarkPresentAsync(int id, string studentId)
    {
        if (!await OwnsSessionAsync(id)) return NotFound();
        var result = await attendanceService.RecordAsync(id, studentId, AttendanceSource.Lecturer);
        TempData[result.Type is CheckInResultType.Recorded or CheckInResultType.AlreadyRecorded ? "StatusMessage" : "ErrorMessage"] = result.Type switch
        {
            CheckInResultType.Recorded => "The student was marked present.",
            CheckInResultType.AlreadyRecorded => "The student was already checked in.",
            CheckInResultType.Expired => "The attendance window has expired.",
            _ => "Attendance could not be recorded."
        };
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnGetQrAsync(int id)
    {
        var session = await FindOwnedSessionAsync(id);
        if (session is null || !WindowIsOpen(session)) return NotFound();
        var remaining = session.AttendanceClosesAtUtc!.Value - DateTime.UtcNow;
        var lifetime = TimeSpan.FromSeconds(Math.Min(attendanceOptions.Value.QrTokenSeconds, Math.Max(1, remaining.TotalSeconds)));
        var token = tokenService.CreateQrToken(id, lifetime);
        var checkInPath = Url.Page("/Student/CheckIn", values: new { token })!;
        var checkInUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{checkInPath}";
        using var qrData = QRCodeGenerator.GenerateQrCode(checkInUrl, QRCodeGenerator.ECCLevel.Q);
        using var renderer = new SvgQRCode(qrData);
        var svg = renderer.GetGraphic(8, "#242423", "#FFFFFF", true, SvgQRCode.SizingMode.ViewBoxAttribute);
        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers["X-Attendance-CheckIn"] = checkInUrl;
        return Content(svg, "image/svg+xml", Encoding.UTF8);
    }

    public async Task<IActionResult> OnGetStatusAsync(int id)
    {
        if (!await OwnsSessionAsync(id)) return NotFound();
        var courseId = await db.LectureSessions.Where(session => session.Id == id).Select(session => session.CourseId).SingleAsync();
        var total = await db.Enrollments.CountAsync(item => item.CourseId == courseId);
        var records = await db.AttendanceRecords.Where(item => item.LectureSessionId == id).OrderByDescending(item => item.CheckedInAtUtc).Select(item => new { item.Student.DisplayName, item.Student.StudentNumber, Status = item.Status.ToString(), Time = institutionTime.ToLocal(item.CheckedInAtUtc).ToString("HH:mm") }).ToListAsync();
        return new JsonResult(new { total, checkedIn = records.Count, records });
    }

    private async Task<bool> LoadAsync(int id)
    {
        var session = await FindOwnedSessionAsync(id);
        if (session is null) return false;
        var isOpen = WindowIsOpen(session);
        Session = new SessionView(session.Id, session.Course.Id, session.Course.Code, session.Course.Name, session.Topic, session.Venue, institutionTime.ToLocal(session.StartsAtUtc), institutionTime.ToLocal(session.EndsAtUtc), isOpen, session.AttendanceClosesAtUtc is null ? null : institutionTime.ToLocal(session.AttendanceClosesAtUtc.Value));
        FallbackCode = isOpen ? tokenService.TryReadFallbackCode(session.FallbackCodeProtected) : null;
        Roster = await db.Enrollments.Where(item => item.CourseId == session.CourseId).OrderBy(item => item.Student.DisplayName).Select(item => new RosterRow(item.StudentId, item.Student.DisplayName, item.Student.StudentNumber ?? "—", item.Course.LectureSessions.SelectMany(lecture => lecture.AttendanceRecords).Any(record => record.LectureSessionId == id && record.StudentId == item.StudentId), item.Course.LectureSessions.SelectMany(lecture => lecture.AttendanceRecords).Where(record => record.LectureSessionId == id && record.StudentId == item.StudentId).Select(record => (AttendanceStatus?)record.Status).FirstOrDefault())).ToListAsync();
        return true;
    }

    private Task<LectureSession?> FindOwnedSessionAsync(int id)
    {
        var userId = userManager.GetUserId(User)!;
        return db.LectureSessions.Include(item => item.Course).FirstOrDefaultAsync(item => item.Id == id && item.Course.Lecturers.Any(link => link.LecturerId == userId));
    }

    private Task<bool> OwnsSessionAsync(int id)
    {
        var userId = userManager.GetUserId(User)!;
        return db.LectureSessions.AnyAsync(item => item.Id == id && item.Course.Lecturers.Any(link => link.LecturerId == userId));
    }

    private static bool WindowIsOpen(LectureSession session) => session.AttendanceState == AttendanceWindowState.Open && session.AttendanceClosesAtUtc > DateTime.UtcNow;

    public sealed record SessionView(int Id, int CourseId, string CourseCode, string CourseName, string Topic, string? Venue, DateTime StartsAt, DateTime EndsAt, bool IsOpen, DateTime? ClosesAt);
    public sealed record RosterRow(string StudentId, string Name, string StudentNumber, bool CheckedIn, AttendanceStatus? Status);
}
