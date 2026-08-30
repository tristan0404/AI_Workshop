using System.ComponentModel.DataAnnotations;
using AI_Workshop.Data;
using AI_Workshop.Models.Academic;
using AI_Workshop.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages.Lecturer.Courses;

public sealed class CreateModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        [Required, StringLength(20), RegularExpression(@"^[A-Za-z0-9-]+$", ErrorMessage = "Use letters, numbers, and hyphens only.")]
        [Display(Name = "Course code")]
        public string Code { get; set; } = string.Empty;
        [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
        [Range(2020, 2100), Display(Name = "Academic year")] public int AcademicYear { get; set; } = DateTime.Now.Year;
        [Range(1, 4)] public int Semester { get; set; } = 1;
        [StringLength(300)] public string? Description { get; set; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var normalizedCode = Input.Code.Trim().ToUpperInvariant();
        var exists = await db.Courses.AnyAsync(course => course.Code == normalizedCode && course.AcademicYear == Input.AcademicYear && course.Semester == Input.Semester);
        if (exists)
        {
            ModelState.AddModelError("Input.Code", "This course offering already exists for the selected year and semester.");
            return Page();
        }

        var course = new Course { Code = normalizedCode, Name = Input.Name.Trim(), AcademicYear = Input.AcademicYear, Semester = Input.Semester, Description = Input.Description?.Trim() };
        course.Lecturers.Add(new CourseLecturer { LecturerId = userManager.GetUserId(User)! });
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        TempData["StatusMessage"] = $"{course.Code} was created.";
        return RedirectToPage("Details", new { id = course.Id });
    }
}
