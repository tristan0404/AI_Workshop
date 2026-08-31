using AI_Workshop.Data;
using AI_Workshop.Models.Attendance;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Pages.Lecturer.Imports;

public sealed class PreviewModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, AttendanceImportService importService) : PageModel
{
    public AttendanceImportBatch Batch { get; private set; } = null!;
    public IReadOnlyList<AttendanceImportItem> PreviewItems { get; private set; } = [];
    public int CreateCount { get; private set; }
    public int UpdateCount { get; private set; }
    public int UnchangedCount { get; private set; }
    public int StudentsToEnrollCount { get; private set; }
    public int StudentsToCreateCount { get; private set; }
    [TempData] public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        return await LoadAsync(id) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostConfirmAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await importService.ConfirmAsync(id, userManager.GetUserId(User)!, cancellationToken);
            SuccessMessage = $"Import complete: {result.Created} attendance records created, {result.Updated} updated, {result.Enrolled} students enrolled, and {result.Provisioned} accounts provisioned.";
            return RedirectToPage(new { id });
        }
        catch (AttendanceImportException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            if (!await LoadAsync(id)) return NotFound();
            return Page();
        }
    }

    private async Task<bool> LoadAsync(int id)
    {
        var lecturerId = userManager.GetUserId(User)!;
        Batch = await db.AttendanceImportBatches.AsNoTracking().Include(batch => batch.Course).Include(batch => batch.Errors)
            .SingleOrDefaultAsync(batch => batch.Id == id && batch.LecturerId == lecturerId) ?? null!;
        if (Batch is null) return false;
        PreviewItems = await db.AttendanceImportItems.AsNoTracking().Where(item => item.BatchId == id)
            .OrderBy(item => item.LectureDate).ThenBy(item => item.StudentNumber).Take(250).ToListAsync();
        var counts = await db.AttendanceImportItems.AsNoTracking().Where(item => item.BatchId == id)
            .GroupBy(item => item.Action).Select(group => new { Action = group.Key, Count = group.Count() }).ToListAsync();
        CreateCount = counts.FirstOrDefault(value => value.Action == AttendanceImportAction.Create)?.Count ?? 0;
        UpdateCount = counts.FirstOrDefault(value => value.Action == AttendanceImportAction.Update)?.Count ?? 0;
        UnchangedCount = counts.FirstOrDefault(value => value.Action == AttendanceImportAction.NoChange)?.Count ?? 0;
        var studentActions = await db.AttendanceImportItems.AsNoTracking().Where(item => item.BatchId == id)
            .Select(item => new { item.StudentNumber, item.StudentAction }).Distinct().ToListAsync();
        StudentsToEnrollCount = studentActions.Count(value => value.StudentAction != AttendanceImportStudentAction.AlreadyEnrolled);
        StudentsToCreateCount = studentActions.Count(value => value.StudentAction == AttendanceImportStudentAction.CreateAndEnroll);
        return true;
    }
}
