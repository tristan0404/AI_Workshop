using System.ComponentModel.DataAnnotations;
using AI_Workshop.Models.Identity;
using AI_Workshop.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Areas.Identity.Pages.Account;

public sealed class RegisterModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext db,
    ILogger<RegisterModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public sealed class InputModel
    {
        [Required, StringLength(120, MinimumLength = 2)]
        [Display(Name = "Full name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required, StringLength(30, MinimumLength = 3)]
        [RegularExpression(@"^[A-Za-z0-9-]+$", ErrorMessage = "Use only letters, numbers, and hyphens.")]
        [Display(Name = "Student number")]
        public string StudentNumber { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/Dashboard");
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return Page();

        var studentNumber = Input.StudentNumber.Trim().ToUpperInvariant();
        var user = await userManager.Users.SingleOrDefaultAsync(value => value.StudentNumber == studentNumber);
        var isActivation = user?.IsProvisioned == true;
        if (user is not null && !isActivation)
        {
            ModelState.AddModelError(nameof(Input.StudentNumber), "An account already exists for this student number.");
            return Page();
        }

        user ??= new ApplicationUser
        {
            StudentNumber = studentNumber
        };
        user.UserName = Input.Email.Trim();
        user.Email = Input.Email.Trim();
        user.DisplayName = Input.DisplayName.Trim();
        user.IsProvisioned = false;

        var result = isActivation
            ? await ActivateProvisionedUserAsync(user, Input.Password)
            : await userManager.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            if (!await userManager.IsInRoleAsync(user, RoleNames.Student))
            {
                var roleResult = await userManager.AddToRoleAsync(user, RoleNames.Student);
                if (!roleResult.Succeeded)
                {
                    if (!isActivation) await userManager.DeleteAsync(user);
                    ModelState.AddModelError(string.Empty, "We could not finish creating the account. Please try again.");
                    return Page();
                }
            }

            logger.LogInformation("A new student account was created.");
            await signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect(returnUrl);
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return Page();
    }

    private async Task<IdentityResult> ActivateProvisionedUserAsync(ApplicationUser user, string password)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded) return updateResult;
        var passwordResult = await userManager.AddPasswordAsync(user, password);
        if (!passwordResult.Succeeded) return passwordResult;
        await transaction.CommitAsync();
        return IdentityResult.Success;
    }
}
