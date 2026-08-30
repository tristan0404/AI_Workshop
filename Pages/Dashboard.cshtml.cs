using AI_Workshop.Data;
using AI_Workshop.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages;

[Authorize]
public sealed class DashboardModel(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : PageModel
{
    public bool IsLecturer { get; private set; }
    public bool IsStudent { get; private set; }
    public string Greeting { get; private set; } = "Welcome to your workspace.";
    public string Introduction { get; private set; } = string.Empty;
    public string RoleLabel { get; private set; } = "Account pending";
    public string TodayLabel => DateTime.Now.ToString("dddd, d MMMM yyyy");
    public int CourseCount { get; private set; }
    public int SessionCount { get; private set; }
    public int PeopleCount { get; private set; }
    public int ReadyStepCount => 1 + (CourseCount > 0 ? 1 : 0) + (PeopleCount > 0 ? 1 : 0);

    public async Task OnGetAsync()
    {
        IsLecturer = User.IsInRole(RoleNames.Lecturer); IsStudent = User.IsInRole(RoleNames.Student);
        RoleLabel = IsLecturer ? "Lecturer" : IsStudent ? "Student" : "Account pending";
        var applicationUser = await userManager.GetUserAsync(User);
        var greetingName = GetGreetingName(applicationUser?.DisplayName);
        Greeting = $"Good {GetTimeOfDay()}, {greetingName}.";
        Introduction = IsLecturer ? "Your teaching workspace is ready for courses, sessions and attendance insights." : "Your attendance workspace is ready for enrolments, check-ins and history.";
        if (applicationUser is null) return;
        if (IsLecturer)
        {
            var courseIds = db.CourseLecturers.Where(item => item.LecturerId == applicationUser.Id).Select(item => item.CourseId);
            CourseCount = await db.Courses.CountAsync(course => courseIds.Contains(course.Id) && !course.IsArchived);
            SessionCount = await db.LectureSessions.CountAsync(session => courseIds.Contains(session.CourseId));
            PeopleCount = await db.Enrollments.Where(item => courseIds.Contains(item.CourseId)).Select(item => item.StudentId).Distinct().CountAsync();
        }
        else if (IsStudent)
        {
            var courseIds = db.Enrollments.Where(item => item.StudentId == applicationUser.Id).Select(item => item.CourseId);
            CourseCount = await courseIds.CountAsync(); SessionCount = await db.LectureSessions.CountAsync(session => courseIds.Contains(session.CourseId)); PeopleCount = CourseCount;
        }
    }

    private static string GetTimeOfDay() => DateTime.Now.Hour < 12 ? "morning" : DateTime.Now.Hour < 18 ? "afternoon" : "evening";

    private static string GetGreetingName(string? displayName)
    {
        var parts = displayName?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (parts.Length == 0) return "there";
        return parts.Length > 1 && (parts[0].Equals("Dr", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("Prof", StringComparison.OrdinalIgnoreCase))
            ? $"{parts[0]} {parts[^1]}"
            : parts[0];
    }
}
