using AI_Workshop.Configuration;
using AI_Workshop.Data;
using AI_Workshop.Models.Academic;
using AI_Workshop.Models.Attendance;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace AI_Workshop.Pages;

[Authorize]
public sealed class DashboardModel(UserManager<ApplicationUser> userManager, ApplicationDbContext db, InstitutionTimeService institutionTime,
    IOptions<AttendanceReportingOptions> reportingOptions, OfficeHoursService officeHoursService) : PageModel
{
    [BindProperty] public OfficeHoursInputModel OfficeHoursInput { get; set; } = new();
    public bool IsLecturer { get; private set; }
    public string Greeting { get; private set; } = "Welcome to your workspace.";
    public string Introduction { get; private set; } = string.Empty;
    public string TodayLabel => DateTime.Now.ToString("dddd, d MMMM yyyy");
    public int SessionCount { get; private set; }
    public int OpenQueryCount { get; private set; }
    public int AtRiskCount { get; private set; }
    public decimal AttendanceRate { get; private set; }
    public decimal AtRiskThreshold => reportingOptions.Value.AtRiskPercentage;
    public LiveSessionView? LiveSession { get; private set; }
    public IReadOnlyList<UpcomingItem> Upcoming { get; private set; } = [];
    public IReadOnlyList<OfficeHoursView> OfficeHoursSlots { get; private set; } = [];

    public sealed class OfficeHoursInputModel
    {
        [EnumDataType(typeof(DayOfWeek)), Display(Name = "Day")]
        public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;
        [DataType(DataType.Time), Display(Name = "Starts")]
        public TimeOnly StartsAt { get; set; } = new(14, 0);
        [DataType(DataType.Time), Display(Name = "Ends")]
        public TimeOnly EndsAt { get; set; } = new(16, 0);
        [Required, StringLength(200, MinimumLength = 2), Display(Name = "Location or meeting link")]
        public string Location { get; set; } = string.Empty;
    }

    public async Task OnGetAsync()
    {
        IsLecturer = User.IsInRole(RoleNames.Lecturer);
        var currentUser = await userManager.GetUserAsync(User);
        Greeting = $"Good {GetTimeOfDay()}, {GetGreetingName(currentUser?.DisplayName)}.";
        Introduction = IsLecturer
            ? "Manage today’s teaching, monitor participation, and act on student queries."
            : "Keep up with today’s lectures, check in quickly, and stay on top of your attendance.";
        if (currentUser is null) return;
        if (IsLecturer) await LoadLecturerAsync(currentUser.Id);
        else if (User.IsInRole(RoleNames.Student)) await LoadStudentAsync(currentUser.Id);
    }

    public async Task<IActionResult> OnPostAddOfficeHoursAsync(CancellationToken cancellationToken)
    {
        if (!User.IsInRole(RoleNames.Lecturer)) return Forbid();
        if (!ModelState.IsValid) { await OnGetAsync(); return Page(); }
        try
        {
            await officeHoursService.CreateAsync(userManager.GetUserId(User)!, OfficeHoursInput.DayOfWeek,
                OfficeHoursInput.StartsAt, OfficeHoursInput.EndsAt, OfficeHoursInput.Location, cancellationToken);
            TempData["StatusMessage"] = "Office hours were published.";
            return RedirectToPage();
        }
        catch (OfficeHoursException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await OnGetAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteOfficeHoursAsync(int id, CancellationToken cancellationToken)
    {
        if (!User.IsInRole(RoleNames.Lecturer)) return Forbid();
        try
        {
            await officeHoursService.DeleteAsync(id, userManager.GetUserId(User)!, cancellationToken);
            TempData["StatusMessage"] = "Office hours were removed.";
        }
        catch (OfficeHoursException exception) { TempData["ErrorMessage"] = exception.Message; }
        return RedirectToPage();
    }

    private async Task LoadLecturerAsync(string lecturerId)
    {
        var now = DateTime.UtcNow;
        var courseIds = await db.CourseLecturers.Where(link => link.LecturerId == lecturerId).Select(link => link.CourseId).ToListAsync();
        OpenQueryCount = await db.AttendanceQueries.CountAsync(query => courseIds.Contains(query.LectureSession.CourseId) &&
            (query.Status == AttendanceQueryStatus.Submitted || query.Status == AttendanceQueryStatus.UnderReview));
        var live = await db.LectureSessions.AsNoTracking().Where(session => courseIds.Contains(session.CourseId) && session.AttendanceState == AttendanceWindowState.Open && session.AttendanceClosesAtUtc > now)
            .OrderBy(session => session.AttendanceClosesAtUtc).Select(session => new { session.Id, session.Course.Code, session.Topic, session.Venue, session.AttendanceClosesAtUtc }).FirstOrDefaultAsync();
        if (live is not null) LiveSession = new LiveSessionView(live.Id, live.Code, live.Topic, live.Venue, Math.Max(1, (int)Math.Ceiling((live.AttendanceClosesAtUtc!.Value - now).TotalMinutes)));
        await LoadOfficeHoursAsync([lecturerId]);
        Upcoming = await LoadUpcomingAsync(courseIds, now, OfficeHoursSlots);

        var sessions = await db.LectureSessions.AsNoTracking().Where(session => courseIds.Contains(session.CourseId) && session.StartsAtUtc <= now && session.Status != LectureSessionStatus.Cancelled)
            .Select(session => new { Enrolled = session.Course.Enrollments.Count, Attended = session.AttendanceRecords.Count(record => record.Status == AttendanceStatus.Present || record.Status == AttendanceStatus.Late) }).ToListAsync();
        SessionCount = sessions.Count;
        AttendanceRate = Percentage(sessions.Sum(value => value.Attended), sessions.Sum(value => value.Enrolled));
        var rates = await db.Enrollments.AsNoTracking().Where(link => courseIds.Contains(link.CourseId)).Select(link => new
        {
            link.StudentId,
            Sessions = link.Course.LectureSessions.Count(session => session.StartsAtUtc <= now && session.Status != LectureSessionStatus.Cancelled),
            Attended = link.Course.LectureSessions.SelectMany(session => session.AttendanceRecords).Count(record => record.StudentId == link.StudentId && (record.Status == AttendanceStatus.Present || record.Status == AttendanceStatus.Late))
        }).ToListAsync();
        AtRiskCount = rates.GroupBy(value => value.StudentId).Count(group => group.Sum(value => value.Sessions) > 0 && group.Sum(value => value.Attended) * 100m / group.Sum(value => value.Sessions) < AtRiskThreshold);
    }

    private async Task LoadStudentAsync(string studentId)
    {
        var now = DateTime.UtcNow;
        var courseIds = await db.Enrollments.Where(link => link.StudentId == studentId).Select(link => link.CourseId).ToListAsync();
        OpenQueryCount = await db.AttendanceQueries.CountAsync(query => query.StudentId == studentId && (query.Status == AttendanceQueryStatus.Submitted || query.Status == AttendanceQueryStatus.UnderReview));
        var live = await db.LectureSessions.AsNoTracking().Where(session => courseIds.Contains(session.CourseId) && session.AttendanceState == AttendanceWindowState.Open && session.AttendanceClosesAtUtc > now)
            .OrderBy(session => session.AttendanceClosesAtUtc).Select(session => new { session.Id, session.Course.Code, session.Topic, session.Venue, session.AttendanceClosesAtUtc }).FirstOrDefaultAsync();
        if (live is not null) LiveSession = new LiveSessionView(live.Id, live.Code, live.Topic, live.Venue, Math.Max(1, (int)Math.Ceiling((live.AttendanceClosesAtUtc!.Value - now).TotalMinutes)));
        var lecturerIds = await db.CourseLecturers.Where(link => courseIds.Contains(link.CourseId)).Select(link => link.LecturerId).Distinct().ToListAsync();
        await LoadOfficeHoursAsync(lecturerIds);
        Upcoming = await LoadUpcomingAsync(courseIds, now, OfficeHoursSlots);
        var sessions = await db.LectureSessions.AsNoTracking().Where(session => courseIds.Contains(session.CourseId) && session.StartsAtUtc <= now && session.Status != LectureSessionStatus.Cancelled)
            .Select(session => session.AttendanceRecords.Where(record => record.StudentId == studentId).Select(record => (AttendanceStatus?)record.Status).FirstOrDefault()).ToListAsync();
        SessionCount = sessions.Count;
        AttendanceRate = Percentage(sessions.Count(status => status is AttendanceStatus.Present or AttendanceStatus.Late), SessionCount);
        AtRiskCount = SessionCount > 0 && AttendanceRate < AtRiskThreshold ? 1 : 0;
    }

    private async Task LoadOfficeHoursAsync(IReadOnlyCollection<string> lecturerIds)
    {
        var localNow = institutionTime.ToLocal(DateTime.UtcNow);
        var slots = await db.OfficeHours.AsNoTracking().Where(slot => lecturerIds.Contains(slot.LecturerId) && slot.IsActive)
            .OrderBy(slot => slot.DayOfWeek).ThenBy(slot => slot.StartsAt)
            .Select(slot => new { slot.Id, slot.LecturerId, LecturerName = slot.Lecturer.DisplayName, slot.DayOfWeek, slot.StartsAt, slot.EndsAt, slot.Location }).ToListAsync();
        OfficeHoursSlots = slots.Select(slot => new OfficeHoursView(slot.Id, slot.LecturerId, slot.LecturerName, slot.DayOfWeek,
            slot.StartsAt, slot.EndsAt, slot.Location, OfficeHoursService.NextOccurrence(slot.DayOfWeek, slot.StartsAt, localNow))).ToList();
    }

    private async Task<IReadOnlyList<UpcomingItem>> LoadUpcomingAsync(IReadOnlyCollection<int> courseIds, DateTime now, IReadOnlyList<OfficeHoursView> officeHours)
    {
        var rows = await db.LectureSessions.AsNoTracking().Where(session => courseIds.Contains(session.CourseId) && session.StartsAtUtc > now && session.Status == LectureSessionStatus.Scheduled)
            .OrderBy(session => session.StartsAtUtc).Take(3).Select(session => new { session.StartsAtUtc, session.Course.Code, session.Topic, session.Venue }).ToListAsync();
        var lectures = rows.Select(row => new UpcomingItem(institutionTime.ToLocal(row.StartsAtUtc), $"{row.Code} · {row.Topic}", row.Venue ?? "Venue to be confirmed"));
        var availability = officeHours.Select(slot => new UpcomingItem(slot.NextOccurrence,
            IsLecturer ? "Office hours" : $"Office hours · {slot.LecturerName}", slot.Location));
        return lectures.Concat(availability).OrderBy(item => item.StartsAt).Take(3).ToList();
    }

    private static decimal Percentage(int value, int total) => total == 0 ? 0 : Math.Round(value * 100m / total, 1);
    private static string GetTimeOfDay() => DateTime.Now.Hour < 12 ? "morning" : DateTime.Now.Hour < 18 ? "afternoon" : "evening";
    private static string GetGreetingName(string? name) { var parts = name?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? []; return parts.Length == 0 ? "there" : parts.Length > 1 && parts[0] is "Dr" or "Prof" ? $"{parts[0]} {parts[^1]}" : parts[0]; }

    public sealed record LiveSessionView(int Id, string CourseCode, string Topic, string? Venue, int MinutesRemaining);
    public sealed record UpcomingItem(DateTime StartsAt, string Title, string Subtitle);
    public sealed record OfficeHoursView(int Id, string LecturerId, string LecturerName, DayOfWeek DayOfWeek, TimeOnly StartsAt,
        TimeOnly EndsAt, string Location, DateTime NextOccurrence);
}
