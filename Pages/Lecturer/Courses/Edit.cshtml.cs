using System.ComponentModel.DataAnnotations;
using AI_Workshop.Data;
using AI_Workshop.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages.Lecturer.Courses;

public sealed class EditModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public int CourseId { get; private set; }

    public sealed class InputModel
    {
        [Required, StringLength(20), RegularExpression(@"^[A-Za-z0-9-]+$")] [Display(Name = "Course code")] public string Code { get; set; } = string.Empty;
        [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
        [Range(2020, 2100), Display(Name = "Academic year")] public int AcademicYear { get; set; }
        [Range(1, 4)] public int Semester { get; set; }
        [StringLength(300)] public string? Description { get; set; }
        [Display(Name = "Archive this course")] public bool IsArchived { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var course = await FindOwnedAsync(id);
        if (course is null) return NotFound();
        CourseId = id;
        Input = new InputModel { Code = course.Code, Name = course.Name, AcademicYear = course.AcademicYear, Semester = course.Semester, Description = course.Description, IsArchived = course.IsArchived };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        CourseId = id;
        if (!ModelState.IsValid) return Page();
        var course = await FindOwnedAsync(id);
        if (course is null) return NotFound();
        var code = Input.Code.Trim().ToUpperInvariant();
        if (await db.Courses.AnyAsync(item => item.Id != id && item.Code == code && item.AcademicYear == Input.AcademicYear && item.Semester == Input.Semester))
        {
            ModelState.AddModelError("Input.Code", "This course offering already exists.");
            return Page();
        }
        course.Code = code; course.Name = Input.Name.Trim(); course.AcademicYear = Input.AcademicYear; course.Semester = Input.Semester; course.Description = Input.Description?.Trim(); course.IsArchived = Input.IsArchived;
        await db.SaveChangesAsync();
        TempData["StatusMessage"] = $"{course.Code} was updated.";
        return RedirectToPage("Details", new { id });
    }

    private Task<Models.Academic.Course?> FindOwnedAsync(int id)
    {
        var userId = userManager.GetUserId(User)!;
        return db.Courses.FirstOrDefaultAsync(course => course.Id == id && course.Lecturers.Any(item => item.LecturerId == userId));
    }
}
