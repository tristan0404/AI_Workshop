using AI_Workshop.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AI_Workshop.Pages;

public sealed class IndexModel(UserManager<ApplicationUser> userManager) : PageModel
{
    public bool IsLecturer { get; private set; }
    public bool IsStudent { get; private set; }
    public string Greeting { get; private set; } = "A clearer way to manage attendance.";
    public string Introduction { get; private set; } = "Sign in to access your role-based workspace.";
    public string RoleLabel { get; private set; } = "Guest";
    public string TodayLabel => DateTime.Now.ToString("dddd, d MMMM yyyy");

    public async Task OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated != true) return;
        IsLecturer = User.IsInRole(RoleNames.Lecturer);
        IsStudent = User.IsInRole(RoleNames.Student);
        RoleLabel = IsLecturer ? "Lecturer" : IsStudent ? "Student" : "Account pending";
        var applicationUser = await userManager.GetUserAsync(User);
        var firstName = applicationUser?.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        Greeting = $"Good {GetTimeOfDay()}, {firstName ?? "there"}.";
        Introduction = IsLecturer ? "Your teaching workspace is ready for courses, sessions and attendance insights." : "Your attendance workspace is ready for enrolments, check-ins and history.";
    }

    private static string GetTimeOfDay() => DateTime.Now.Hour < 12 ? "morning" : DateTime.Now.Hour < 18 ? "afternoon" : "evening";
}
