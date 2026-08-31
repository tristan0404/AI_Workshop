using System.Globalization;
using AI_Workshop.Configuration;
using AI_Workshop.Data;
using AI_Workshop.Models.Academic;
using AI_Workshop.Models.Attendance;
using AI_Workshop.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AI_Workshop.Services;

public sealed class AttendanceImportService(
    ApplicationDbContext db,
    IAttendanceSpreadsheetReader spreadsheetReader,
    InstitutionTimeService institutionTime,
    IOptions<AttendanceImportOptions> options,
    UserManager<ApplicationUser> userManager)
{
    private static readonly string[] NameHeaders = ["student name", "name", "full name"];
    private static readonly string[] NumberHeaders = ["student number", "student no", "student id", "studentnumber"];
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "d MMM yyyy", "dd MMM yyyy"];
    private readonly AttendanceImportOptions _options = options.Value;

    public async Task<int> PreviewAsync(int courseId, string lecturerId, string fileName, Stream stream, long length, CancellationToken cancellationToken)
    {
        if (length <= 0) throw new AttendanceImportException("Choose a non-empty attendance file.");
        if (length > _options.MaximumFileBytes) throw new AttendanceImportException($"The file must be smaller than {_options.MaximumFileBytes / 1_000_000d:0.#} MB.");
        var safeFileName = Path.GetFileName(fileName);
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (extension is not ".csv" and not ".xlsx") throw new AttendanceImportException("Only .csv and .xlsx files are supported.");

        var ownsCourse = await db.CourseLecturers.AnyAsync(link => link.CourseId == courseId && link.LecturerId == lecturerId, cancellationToken);
        if (!ownsCourse) throw new AttendanceImportException("The selected course was not found or is not assigned to you.");

        var table = await spreadsheetReader.ReadAsync(stream, extension, _options.MaximumRows, cancellationToken);
        var batch = new AttendanceImportBatch { CourseId = courseId, LecturerId = lecturerId, FileName = safeFileName, SpreadsheetRowCount = table.Rows.Count };
        db.AttendanceImportBatches.Add(batch);
        ValidateAndStage(table, batch, await LoadContextAsync(courseId, cancellationToken));
        batch.RecordCount = batch.Items.Count;
        batch.ErrorCount = batch.Errors.Count;
        await db.SaveChangesAsync(cancellationToken);
        return batch.Id;
    }

    public async Task<ImportConfirmationResult> ConfirmAsync(int batchId, string lecturerId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var batch = await db.AttendanceImportBatches.Include(value => value.Items)
            .SingleOrDefaultAsync(value => value.Id == batchId && value.LecturerId == lecturerId, cancellationToken)
            ?? throw new AttendanceImportException("Import preview not found.");
        if (batch.Status != AttendanceImportStatus.Previewed) throw new AttendanceImportException("This import has already been processed.");
        if (batch.ErrorCount > 0) throw new AttendanceImportException("Resolve the validation errors and upload the file again before importing.");

        var studentNumbers = batch.Items.Select(item => item.StudentNumber).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var users = await db.Users.Where(user => user.StudentNumber != null && studentNumbers.Contains(user.StudentNumber))
            .ToDictionaryAsync(user => user.StudentNumber!, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var provisioned = 0;
        var enrolled = 0;
        foreach (var studentGroup in batch.Items.GroupBy(item => item.StudentNumber, StringComparer.OrdinalIgnoreCase))
        {
            var firstItem = studentGroup.First();
            if (!users.TryGetValue(studentGroup.Key, out var student))
            {
                var provisionalEmail = $"pending-{Guid.NewGuid():N}@attendly.invalid";
                student = new ApplicationUser
                {
                    UserName = provisionalEmail,
                    Email = provisionalEmail,
                    DisplayName = firstItem.StudentName,
                    StudentNumber = firstItem.StudentNumber,
                    IsProvisioned = true
                };
                EnsureIdentitySucceeded(await userManager.CreateAsync(student), $"provision {firstItem.StudentNumber}");
                EnsureIdentitySucceeded(await userManager.AddToRoleAsync(student, RoleNames.Student), $"assign the student role to {firstItem.StudentNumber}");
                users[firstItem.StudentNumber] = student;
                provisioned++;
            }
            else if (!await userManager.IsInRoleAsync(student, RoleNames.Student))
            {
                EnsureIdentitySucceeded(await userManager.AddToRoleAsync(student, RoleNames.Student), $"assign the student role to {firstItem.StudentNumber}");
            }

            if (!await db.Enrollments.AnyAsync(link => link.CourseId == batch.CourseId && link.StudentId == student.Id, cancellationToken))
            {
                db.Enrollments.Add(new Enrollment { CourseId = batch.CourseId, StudentId = student.Id });
                enrolled++;
            }
            foreach (var item in studentGroup) item.StudentId = student.Id;
        }

        var dates = batch.Items.Select(item => item.LectureDate.Date).Distinct().Order().ToList();
        var sessions = new Dictionary<DateTime, LectureSession>();
        foreach (var date in dates)
        {
            var matching = await SessionsOnDateAsync(batch.CourseId, date, cancellationToken);
            if (matching.Count > 1) throw new AttendanceImportException($"More than one lecture exists on {date:d MMM yyyy}; the import is now ambiguous.");
            var session = matching.SingleOrDefault();
            if (session is null)
            {
                var localStart = date.AddHours(_options.HistoricalSessionStartHour);
                session = new LectureSession
                {
                    CourseId = batch.CourseId,
                    Topic = $"Historical lecture · {date:d MMM yyyy}",
                    StartsAtUtc = institutionTime.ToUtc(localStart),
                    EndsAtUtc = institutionTime.ToUtc(localStart.AddMinutes(_options.HistoricalSessionDurationMinutes)),
                    Status = LectureSessionStatus.Completed
                };
                db.LectureSessions.Add(session);
            }
            sessions[date] = session;
        }

        var studentIds = batch.Items.Select(item => item.StudentId!).Distinct().ToList();
        var sessionIds = sessions.Values.Where(session => session.Id > 0).Select(session => session.Id).ToList();
        var existing = await db.AttendanceRecords.Where(record => studentIds.Contains(record.StudentId) && sessionIds.Contains(record.LectureSessionId))
            .ToDictionaryAsync(record => (record.LectureSessionId, record.StudentId), cancellationToken);
        var created = 0;
        var updated = 0;
        foreach (var item in batch.Items)
        {
            var session = sessions[item.LectureDate.Date];
            AttendanceRecord? record = null;
            if (session.Id > 0) existing.TryGetValue((session.Id, item.StudentId!), out record);
            if (record is null)
            {
                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    LectureSession = session, StudentId = item.StudentId!, Status = item.Status,
                    Source = AttendanceSource.Import, CheckedInAtUtc = session.StartsAtUtc
                });
                created++;
            }
            else if (record.Status != item.Status)
            {
                record.Status = item.Status;
                record.Source = AttendanceSource.Import;
                record.RequiresReview = false;
                updated++;
            }
        }
        batch.Status = AttendanceImportStatus.Completed;
        batch.ConfirmedAtUtc = DateTime.UtcNow;
        batch.CreatedCount = created;
        batch.UpdatedCount = updated;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ImportConfirmationResult(created, updated, batch.Items.Count - created - updated, enrolled, provisioned);
    }

    private void ValidateAndStage(SpreadsheetTable table, AttendanceImportBatch batch, ImportContext context)
    {
        if (table.Headers.Count < 3) { Error(batch, null, null, "Expected Student Name, Student Number, and at least one lecture date column."); return; }
        var normalized = table.Headers.Select(NormalizeHeader).ToList();
        var nameIndex = normalized.FindIndex(NameHeaders.Contains);
        var numberIndex = normalized.FindIndex(NumberHeaders.Contains);
        if (nameIndex < 0) Error(batch, 1, null, "A Student Name column is required.");
        if (numberIndex < 0) Error(batch, 1, null, "A Student Number column is required.");
        if (nameIndex < 0 || numberIndex < 0) return;

        var dateColumns = new List<(int Index, DateTime Date)>();
        for (var index = 0; index < table.Headers.Count; index++)
        {
            if (index == nameIndex || index == numberIndex) continue;
            if (TryParseDate(table.Headers[index], out var date)) dateColumns.Add((index, date));
            else Error(batch, 1, table.Headers[index], "This column heading is not a supported lecture date.");
        }
        var duplicateDates = dateColumns.GroupBy(value => value.Date).Where(group => group.Count() > 1).ToList();
        foreach (var duplicate in duplicateDates)
            Error(batch, 1, duplicate.Key.ToString("yyyy-MM-dd"), "This lecture date appears more than once.");
        if (duplicateDates.Count > 0) return;
        if (dateColumns.Count == 0) { Error(batch, 1, null, "At least one valid lecture date column is required."); return; }

        var seenStudents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows)
        {
            var name = Cell(row, nameIndex);
            var number = Cell(row, numberIndex);
            if (string.IsNullOrWhiteSpace(name)) Error(batch, row.Number, table.Headers[nameIndex], "Student name is required.");
            if (string.IsNullOrWhiteSpace(number)) { Error(batch, row.Number, table.Headers[numberIndex], "Student number is required."); continue; }
            if (!seenStudents.Add(number)) { Error(batch, row.Number, table.Headers[numberIndex], "This student appears more than once."); continue; }
            var normalizedNumber = number.Trim().ToUpperInvariant();
            context.Students.TryGetValue(normalizedNumber, out var student);
            var actualName = student?.DisplayName.Trim() ?? name.Trim();
            if (student is not null && !string.IsNullOrWhiteSpace(name) && !NamesMatch(name, actualName)) Error(batch, row.Number, table.Headers[nameIndex], $"Name does not match {actualName} for this student number.");
            var studentAction = student is null
                ? AttendanceImportStudentAction.CreateAndEnroll
                : context.EnrolledStudentIds.Contains(student.Id) ? AttendanceImportStudentAction.AlreadyEnrolled : AttendanceImportStudentAction.EnrollExisting;

            foreach (var column in dateColumns)
            {
                var value = Cell(row, column.Index);
                if (!TryParseStatus(value, out var status)) { Error(batch, row.Number, table.Headers[column.Index], "Use 1 for present or 0 for absent."); continue; }
                var sessions = context.Sessions.GetValueOrDefault(column.Date) ?? [];
                if (sessions.Count > 1) { Error(batch, row.Number, table.Headers[column.Index], "More than one lecture exists on this date, so the record is ambiguous."); continue; }
                var session = sessions.SingleOrDefault();
                var existing = student is null ? null : session?.AttendanceRecords.SingleOrDefault(record => record.StudentId == student.Id);
                batch.Items.Add(new AttendanceImportItem
                {
                    SpreadsheetRow = row.Number, StudentId = student?.Id, StudentNumber = normalizedNumber,
                    StudentName = actualName, LectureDate = column.Date, LectureSessionId = session?.Id, Status = status,
                    Action = existing is null ? AttendanceImportAction.Create : existing.Status == status ? AttendanceImportAction.NoChange : AttendanceImportAction.Update,
                    StudentAction = studentAction
                });
            }
        }
    }

    private async Task<ImportContext> LoadContextAsync(int courseId, CancellationToken cancellationToken)
    {
        var students = await db.Users.Where(user => user.StudentNumber != null).ToListAsync(cancellationToken);
        var enrolledStudentIds = await db.Enrollments.Where(link => link.CourseId == courseId).Select(link => link.StudentId).ToHashSetAsync(cancellationToken);
        var sessions = await db.LectureSessions.Where(session => session.CourseId == courseId)
            .Include(session => session.AttendanceRecords).ToListAsync(cancellationToken);
        return new ImportContext(students.ToDictionary(student => student.StudentNumber!, StringComparer.OrdinalIgnoreCase), enrolledStudentIds,
            sessions.GroupBy(session => institutionTime.ToLocal(session.StartsAtUtc).Date).ToDictionary(group => group.Key, group => group.ToList()));
    }

    private async Task<List<LectureSession>> SessionsOnDateAsync(int courseId, DateTime date, CancellationToken cancellationToken)
    {
        var start = institutionTime.ToUtc(date);
        var end = institutionTime.ToUtc(date.AddDays(1));
        return await db.LectureSessions.Where(session => session.CourseId == courseId && session.StartsAtUtc >= start && session.StartsAtUtc < end).ToListAsync(cancellationToken);
    }

    private static string Cell(SpreadsheetRow row, int index) => index < row.Cells.Count ? row.Cells[index].Trim() : string.Empty;
    private static string NormalizeHeader(string value) => string.Join(' ', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    private static bool TryParseDate(string value, out DateTime date) => DateTime.TryParseExact(value.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    private static bool TryParseStatus(string value, out AttendanceStatus status) { status = value.Trim() == "1" ? AttendanceStatus.Present : AttendanceStatus.Absent; return value.Trim() is "0" or "1"; }
    private static bool NamesMatch(string supplied, string actual) => NormalizeHeader(supplied) == NormalizeHeader(actual);
    private static void EnsureIdentitySucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded) return;
        throw new AttendanceImportException($"Unable to {operation}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
    }
    private static void Error(AttendanceImportBatch batch, int? row, string? column, string message) => batch.Errors.Add(new AttendanceImportError { SpreadsheetRow = row, Column = column, Message = message });
    private sealed record ImportContext(Dictionary<string, ApplicationUser> Students, HashSet<string> EnrolledStudentIds, Dictionary<DateTime, List<LectureSession>> Sessions);
}

public sealed record ImportConfirmationResult(int Created, int Updated, int Unchanged, int Enrolled, int Provisioned);
