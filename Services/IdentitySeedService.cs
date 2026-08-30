using AI_Workshop.Models.Identity;
using Microsoft.AspNetCore.Identity;

namespace AI_Workshop.Services;

public static class IdentitySeedService
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(IdentitySeedService));
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        logger.LogInformation("Checking application roles.");
        foreach (var roleName in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                EnsureSucceeded(result, $"create the {roleName} role");
            }
        }

        if (!configuration.GetValue<bool>("SeedData:Enabled"))
        {
            return;
        }

        logger.LogInformation("Creating development demo users when needed.");
        var lecturerPassword = configuration["SeedData:LecturerPassword"]
            ?? throw new InvalidOperationException("SeedData:LecturerPassword is required when demo seeding is enabled.");
        var studentPassword = configuration["SeedData:StudentPassword"]
            ?? throw new InvalidOperationException("SeedData:StudentPassword is required when demo seeding is enabled.");

        await CreateUserAsync(
            userManager,
            configuration["SeedData:LecturerEmail"] ?? "lecturer@attendly.local",
            lecturerPassword,
            "Dr Maya Naidoo",
            RoleNames.Lecturer);

        await CreateUserAsync(
            userManager,
            configuration["SeedData:StudentEmail"] ?? "student@attendly.local",
            studentPassword,
            "Thabo Mokoena",
            RoleNames.Student,
            "STU-2026-001");

        logger.LogInformation("Development roles and demo users are ready.");
    }

    private static async Task CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string displayName,
        string role,
        string? studentNumber = null)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
                StudentNumber = studentNumber
            };

            var createResult = await userManager.CreateAsync(user, password);
            EnsureSucceeded(createResult, $"create demo user {email}");
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var roleResult = await userManager.AddToRoleAsync(user, role);
            EnsureSucceeded(roleResult, $"assign {email} to {role}");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Unable to {operation}: {errors}");
    }
}
