using AI_Workshop.Configuration;
using AI_Workshop.Data;
using AI_Workshop.Models.Attendance;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AI_Workshop.Pages;

[Authorize]
public sealed class DashboardModel(UserManager<ApplicationUser> userManager, ApplicationDbContext db, InstitutionTimeService institutionTime, IOptions<AttendanceReportingOptions> reportingOptions) : PageModel
{
    public bool IsLecturer { get; private set; }
    public bool IsStudent { get; private set; }
    public string Greeting { get; private set; } = "Welcome to your workspace.";
    public string Introduction { get; private set; } = string.Empty;
    public string TodayLabel => DateTime.Now.ToString("dddd, d MMMM yyyy");
    public int CourseCount { get; private set; }
    public int SessionCount { get; private set; }
    public int PeopleCount { get; private set; }
    public int OpenQueryCount { get; private set; }
    public int AtRiskCount { get; private set; }
    public decimal AttendanceRate { get; private set; }
    public decimal AtRiskThreshold => reportingOptions.Value.AtRiskPercentage;
    public IReadOnlyList<TrendPoint> Trend { get; private set; } = [];
    public IReadOnlyList<BarPoint> Bars { get; private set; } = [];
    public IReadOnlyList<DonutPoint> Donut { get; private set; } = [];
    public IReadOnlyList<HeatPoint> Heatmap { get; private set; } = [];

    public async Task OnGetAsync()
    {
        IsLecturer = User.IsInRole(RoleNames.Lecturer);
        IsStudent = User.IsInRole(RoleNames.Student);
        var currentUser = await userManager.GetUserAsync(User);
        Greeting = $"Good {GetTimeOfDay()}, {GetGreetingName(currentUser?.DisplayName)}.";
        Introduction = IsLecturer ? "Monitor participation, spot attendance risk, and act on student queries." : "Understand your attendance pattern and stay ahead of course requirements.";
        if (currentUser is null) return;
        if (IsLecturer) await LoadLecturerAsync(currentUser.Id);
        else if (IsStudent) await LoadStudentAsync(currentUser.Id);
    }

    private async Task LoadLecturerAsync(string lecturerId)
    {
        var courseIds = await db.CourseLecturers.Where(link => link.LecturerId == lecturerId).Select(link => link.CourseId).ToListAsync();
        CourseCount = await db.Courses.CountAsync(course => courseIds.Contains(course.Id) && !course.IsArchived);
        PeopleCount = await db.Enrollments.Where(link => courseIds.Contains(link.CourseId)).Select(link => link.StudentId).Distinct().CountAsync();
        OpenQueryCount = await db.AttendanceQueries.CountAsync(query => courseIds.Contains(query.LectureSession.CourseId) && (query.Status == AttendanceQueryStatus.Submitted || query.Status == AttendanceQueryStatus.UnderReview));
        var sessions = await db.LectureSessions.AsNoTracking().Where(session => courseIds.Contains(session.CourseId) && session.StartsAtUtc <= DateTime.UtcNow && session.Status != Models.Academic.LectureSessionStatus.Cancelled)
            .OrderBy(session => session.StartsAtUtc).Select(session => new SessionData(session.Course.Code, session.Topic, session.StartsAtUtc,
                session.Course.Enrollments.Count, session.AttendanceRecords.Count(record => record.Status == AttendanceStatus.Present),
                session.AttendanceRecords.Count(record => record.Status == AttendanceStatus.Late), session.AttendanceRecords.Count(record => record.Status == AttendanceStatus.Absent),
                session.AttendanceRecords.Count(record => record.Status == AttendanceStatus.Excused))).ToListAsync();
        SessionCount = sessions.Count;
        BuildSessionCharts(sessions);

        var rates = await db.Enrollments.AsNoTracking().Where(link => courseIds.Contains(link.CourseId)).Select(link => new
        {
            link.StudentId,
            Sessions = link.Course.LectureSessions.Count(session => session.StartsAtUtc <= DateTime.UtcNow && session.Status != Models.Academic.LectureSessionStatus.Cancelled),
            Attended = link.Course.LectureSessions.SelectMany(session => session.AttendanceRecords).Count(record => record.StudentId == link.StudentId && (record.Status == AttendanceStatus.Present || record.Status == AttendanceStatus.Late))
        }).ToListAsync();
        AtRiskCount = rates.GroupBy(value => value.StudentId).Count(group =>
        {
            var total = group.Sum(value => value.Sessions);
            return total > 0 && group.Sum(value => value.Attended) * 100m / total < AtRiskThreshold;
        });
    }

    private async Task LoadStudentAsync(string studentId)
    {
        var courseIds = await db.Enrollments.Where(link => link.StudentId == studentId).Select(link => link.CourseId).ToListAsync();
        CourseCount = courseIds.Count;
        PeopleCount = CourseCount;
        OpenQueryCount = await db.AttendanceQueries.CountAsync(query => query.StudentId == studentId && (query.Status == AttendanceQueryStatus.Submitted || query.Status == AttendanceQueryStatus.UnderReview));
        var sessions = await db.LectureSessions.AsNoTracking().Where(session => courseIds.Contains(session.CourseId) && session.StartsAtUtc <= DateTime.UtcNow && session.Status != Models.Academic.LectureSessionStatus.Cancelled)
            .OrderBy(session => session.StartsAtUtc).Select(session => new
            {
                session.CourseId, session.Course.Code, session.Topic, session.StartsAtUtc,
                Status = session.AttendanceRecords.Where(record => record.StudentId == studentId).Select(record => (AttendanceStatus?)record.Status).FirstOrDefault()
            }).ToListAsync();
        SessionCount = sessions.Count;
        var attended = sessions.Count(value => value.Status is AttendanceStatus.Present or AttendanceStatus.Late);
        AttendanceRate = Percentage(attended, SessionCount);
        AtRiskCount = SessionCount > 0 && AttendanceRate < AtRiskThreshold ? 1 : 0;
        Trend = sessions.TakeLast(12).Select(value => new TrendPoint(institutionTime.ToLocal(value.StartsAtUtc).ToString("d MMM"), value.Status is AttendanceStatus.Present or AttendanceStatus.Late ? 100 : 0, $"{value.Code} · {value.Topic}")).ToList();
        Bars = sessions.GroupBy(value => new { value.CourseId, value.Code }).Select(group => new BarPoint(group.Key.Code,
            group.Count(value => value.Status is AttendanceStatus.Present or AttendanceStatus.Late),
            group.Count(value => value.Status is not AttendanceStatus.Present and not AttendanceStatus.Late), group.Key.Code)).ToList();
        Donut = BuildDonut(sessions.Select(value => value.Status));
        Heatmap = sessions.GroupBy(value => institutionTime.ToLocal(value.StartsAtUtc).Date).Select(group => new HeatPoint(group.Key.ToString("yyyy-MM-dd"),
            Percentage(group.Count(value => value.Status is AttendanceStatus.Present or AttendanceStatus.Late), group.Count()), group.Count())).ToList();
    }

    private void BuildSessionCharts(IReadOnlyList<SessionData> sessions)
    {
        var expected = sessions.Sum(value => value.Enrolled);
        AttendanceRate = Percentage(sessions.Sum(value => value.Present + value.Late), expected);
        Trend = sessions.TakeLast(12).Select(value => new TrendPoint(institutionTime.ToLocal(value.StartsAtUtc).ToString("d MMM"), Percentage(value.Present + value.Late, value.Enrolled), $"{value.CourseCode} · {value.Topic}")).ToList();
        Bars = sessions.TakeLast(8).Select(value => new BarPoint(institutionTime.ToLocal(value.StartsAtUtc).ToString("d MMM"), value.Present + value.Late, Math.Max(0, value.Enrolled - value.Present - value.Late), value.CourseCode)).ToList();
        Donut = [new("Present", sessions.Sum(value => value.Present)), new("Late", sessions.Sum(value => value.Late)), new("Absent", sessions.Sum(value => Math.Max(value.Absent, value.Enrolled - value.Present - value.Late - value.Excused))), new("Excused", sessions.Sum(value => value.Excused))];
        Heatmap = sessions.GroupBy(value => institutionTime.ToLocal(value.StartsAtUtc).Date).Select(group => new HeatPoint(group.Key.ToString("yyyy-MM-dd"), Percentage(group.Sum(value => value.Present + value.Late), group.Sum(value => value.Enrolled)), group.Count())).ToList();
    }

    private static IReadOnlyList<DonutPoint> BuildDonut(IEnumerable<AttendanceStatus?> statuses) =>
        [new("Present", statuses.Count(value => value == AttendanceStatus.Present)), new("Late", statuses.Count(value => value == AttendanceStatus.Late)), new("Absent", statuses.Count(value => value is null or AttendanceStatus.Absent)), new("Excused", statuses.Count(value => value == AttendanceStatus.Excused))];
    private static decimal Percentage(int value, int total) => total == 0 ? 0 : Math.Round(value * 100m / total, 1);
    private static string GetTimeOfDay() => DateTime.Now.Hour < 12 ? "morning" : DateTime.Now.Hour < 18 ? "afternoon" : "evening";
    private static string GetGreetingName(string? name) { var parts = name?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? []; return parts.Length == 0 ? "there" : parts.Length > 1 && parts[0] is "Dr" or "Prof" ? $"{parts[0]} {parts[^1]}" : parts[0]; }

    private sealed record SessionData(string CourseCode, string Topic, DateTime StartsAtUtc, int Enrolled, int Present, int Late, int Absent, int Excused);
    public sealed record TrendPoint(string Label, decimal Value, string Detail);
    public sealed record BarPoint(string Label, int Attended, int Missed, string Detail);
    public sealed record DonutPoint(string Label, int Value);
    public sealed record HeatPoint(string Date, decimal Value, int Sessions);
}
