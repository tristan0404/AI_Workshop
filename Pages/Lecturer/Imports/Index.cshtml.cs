using System.ComponentModel.DataAnnotations;
using System.Text;
using AI_Workshop.Data;
using AI_Workshop.Models.Attendance;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages.Lecturer.Imports;

public sealed class IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, AttendanceImportService importService) : PageModel
{
    [BindProperty] public UploadInput Input { get; set; } = new();
    public IReadOnlyList<SelectListItem> Courses { get; private set; } = [];
    public IReadOnlyList<ImportRow> Imports { get; private set; } = [];

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadAsync();
        if (!ModelState.IsValid) return Page();
        try
        {
            await using var stream = Input.File!.OpenReadStream();
            var batchId = await importService.PreviewAsync(Input.CourseId, userManager.GetUserId(User)!, Input.File.FileName, stream, Input.File.Length, cancellationToken);
            return RedirectToPage("Preview", new { id = batchId });
        }
        catch (AttendanceImportException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    public IActionResult OnGetTemplate()
    {
        const string csv = "Student Name,Student Number,2026-02-10,2026-02-17\r\nExample Student,STU001,1,0\r\n";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "attendance-import-template.csv");
    }

    private async Task LoadAsync()
    {
        var lecturerId = userManager.GetUserId(User)!;
        Courses = await db.Courses.AsNoTracking().Where(course => !course.IsArchived && course.Lecturers.Any(link => link.LecturerId == lecturerId))
            .OrderBy(course => course.Code).Select(course => new SelectListItem($"{course.Code} · {course.Name}", course.Id.ToString())).ToListAsync();
        Imports = await db.AttendanceImportBatches.AsNoTracking().Where(batch => batch.LecturerId == lecturerId)
            .OrderByDescending(batch => batch.UploadedAtUtc).Take(20)
            .Select(batch => new ImportRow(batch.Id, batch.Course.Code, batch.FileName, batch.Status, batch.RecordCount, batch.ErrorCount, batch.UploadedAtUtc))
            .ToListAsync();
    }

    public sealed class UploadInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "Choose a course.")] public int CourseId { get; set; }
        [Required(ErrorMessage = "Choose a CSV or Excel file.")] public IFormFile? File { get; set; }
    }
    public sealed record ImportRow(int Id, string CourseCode, string FileName, AttendanceImportStatus Status, int Records, int Errors, DateTime UploadedAtUtc);
}
