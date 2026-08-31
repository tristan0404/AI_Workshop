using System.ComponentModel.DataAnnotations;
using AI_Workshop.Configuration;
using AI_Workshop.Data;
using AI_Workshop.Models.Academic;
using AI_Workshop.Models.Attendance;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

var tests = new (string Name, Func<Task> Run)[]
{
    ("QR tokens round-trip and reject tampering", TestQrTokensAsync),
    ("Institution time conversion round-trips", TestInstitutionTimeAsync),
    ("Attendance records present and late check-ins", TestAttendanceClassificationAsync),
    ("Attendance rejects unenrolled students and duplicate check-ins", TestAttendanceGuardsAsync),
    ("CSV reader handles quoted names and semicolon files", TestCsvReaderAsync),
    ("Attendance queries prevent duplicates and create an audit trail", TestAttendanceReviewAsync),
    ("Attendance configuration rejects invalid ranges", TestConfigurationValidationAsync)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add(test.Name);
        Console.WriteLine($"FAIL  {test.Name}\n      {exception.Message}");
    }
}

Console.WriteLine($"\n{tests.Length - failures.Count}/{tests.Length} quality checks passed.");
return failures.Count == 0 ? 0 : 1;

static Task TestQrTokensAsync()
{
    var service = new AttendanceTokenService(new EphemeralDataProtectionProvider());
    var token = service.CreateQrToken(42, TimeSpan.FromMinutes(1));
    Assert(service.TryReadQrToken(token, out var sessionId) && sessionId == 42, "A valid QR token was not accepted.");
    Assert(!service.TryReadQrToken(token + "tampered", out _), "A modified QR token was accepted.");
    var protectedCode = service.ProtectFallbackCode("123456");
    Assert(service.TryReadFallbackCode(protectedCode) == "123456", "The fallback code did not decrypt correctly.");
    return Task.CompletedTask;
}

static Task TestInstitutionTimeAsync()
{
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Institution:TimeZoneId"] = "Africa/Johannesburg"
    }).Build();
    var service = new InstitutionTimeService(configuration);
    var local = new DateTime(2026, 8, 31, 9, 30, 0, DateTimeKind.Unspecified);
    Assert(service.ToLocal(service.ToUtc(local)) == local, "Local time did not survive a UTC round trip.");
    return Task.CompletedTask;
}

static async Task TestAttendanceClassificationAsync()
{
    await using var fixture = await AttendanceFixture.CreateAsync();
    var present = await fixture.Service.RecordAsync(fixture.PresentSessionId, fixture.StudentId, AttendanceSource.QrCode);
    var late = await fixture.Service.RecordAsync(fixture.LateSessionId, fixture.StudentId, AttendanceSource.FallbackCode);
    Assert(present.Type == CheckInResultType.Recorded && present.Record?.Status == AttendanceStatus.Present,
        "A check-in before the late boundary was not marked present.");
    Assert(late.Type == CheckInResultType.Recorded && late.Record?.Status == AttendanceStatus.Late,
        "A check-in after the late boundary was not marked late.");
}

static async Task TestAttendanceGuardsAsync()
{
    await using var fixture = await AttendanceFixture.CreateAsync();
    var first = await fixture.Service.RecordAsync(fixture.PresentSessionId, fixture.StudentId, AttendanceSource.QrCode);
    var duplicate = await fixture.Service.RecordAsync(fixture.PresentSessionId, fixture.StudentId, AttendanceSource.QrCode);
    var outsider = await fixture.Service.RecordAsync(fixture.PresentSessionId, "not-enrolled", AttendanceSource.QrCode);
    Assert(first.Type == CheckInResultType.Recorded, "The initial check-in failed.");
    Assert(duplicate.Type == CheckInResultType.AlreadyRecorded, "A duplicate check-in created another record.");
    Assert(outsider.Type == CheckInResultType.NotEnrolled, "An unenrolled student was permitted to check in.");
}

static async Task TestCsvReaderAsync()
{
    const string csv = "Student Name;Student Number;2026-08-31\n\"Mokoena; Thabo\";STU001;1\n";
    await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
    var table = await new AttendanceSpreadsheetReader().ReadAsync(stream, ".csv", 10, CancellationToken.None);
    Assert(table.Headers.Count == 3 && table.Rows.Count == 1, "The CSV dimensions were parsed incorrectly.");
    Assert(table.Rows[0].Cells[0] == "Mokoena; Thabo", "A quoted delimiter was treated as a column break.");
}

static async Task TestAttendanceReviewAsync()
{
    await using var fixture = await AttendanceFixture.CreateAsync();
    var review = new AttendanceReviewService(fixture.Db);
    var queryId = await review.SubmitAsync(fixture.PresentSessionId, fixture.StudentId, AttendanceStatus.Present,
        "I attended this lecture and checked in.", null, null, null, 0, CancellationToken.None);
    await ExpectAsync<AttendanceReviewException>(() => review.SubmitAsync(fixture.PresentSessionId, fixture.StudentId,
        AttendanceStatus.Present, "This duplicate query must be rejected.", null, null, null, 0, CancellationToken.None));
    await review.ResolveAsync(queryId, fixture.LecturerId, true, "Verified against the class register.", CancellationToken.None);
    var query = await fixture.Db.AttendanceQueries.SingleAsync(item => item.Id == queryId);
    var auditCount = await fixture.Db.AttendanceChangeLogs.CountAsync(item => item.AttendanceQueryId == queryId);
    Assert(query.Status == AttendanceQueryStatus.Approved && auditCount == 1,
        "Approving a query did not create exactly one audit entry.");
}

static Task TestConfigurationValidationAsync()
{
    var options = new AttendanceOptions { WindowMinutes = 0, LateAfterMinutes = 5, QrTokenSeconds = 2 };
    var context = new ValidationContext(options);
    var results = new List<ValidationResult>();
    Assert(!Validator.TryValidateObject(options, context, results, true), "Invalid attendance options passed validation.");
    return Task.CompletedTask;
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task ExpectAsync<TException>(Func<Task> action) where TException : Exception
{
    try { await action(); }
    catch (TException) { return; }
    throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
}

sealed class AttendanceFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    public ApplicationDbContext Db { get; }
    public AttendanceService Service { get; }
    public string StudentId { get; } = "student-1";
    public string LecturerId { get; } = "lecturer-1";
    public int PresentSessionId { get; private set; }
    public int LateSessionId { get; private set; }

    private AttendanceFixture(SqliteConnection connection, ApplicationDbContext db)
    {
        _connection = connection;
        Db = db;
        Service = new AttendanceService(db, Options.Create(new AttendanceOptions
        {
            WindowMinutes = 10,
            LateAfterMinutes = 5,
            QrTokenSeconds = 35
        }));
    }

    public static async Task<AttendanceFixture> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = new AttendanceFixture(connection, db);
        var course = new Course { Code = "TST101", Name = "Quality Testing", AcademicYear = 2026, Semester = 1 };
        course.Enrollments.Add(new Enrollment
        {
            StudentId = fixture.StudentId,
            Student = new ApplicationUser { Id = fixture.StudentId, UserName = "student@test.local", DisplayName = "Test Student", StudentNumber = "TEST001" }
        });
        course.Lecturers.Add(new CourseLecturer
        {
            LecturerId = fixture.LecturerId,
            Lecturer = new ApplicationUser { Id = fixture.LecturerId, UserName = "lecturer@test.local", DisplayName = "Test Lecturer" }
        });
        var now = DateTime.UtcNow;
        var presentSession = OpenSession(course, "Present test", now.AddMinutes(-1), now.AddMinutes(9));
        var lateSession = OpenSession(course, "Late test", now.AddMinutes(-7), now.AddMinutes(3));
        db.Courses.Add(course);
        db.LectureSessions.AddRange(presentSession, lateSession);
        await db.SaveChangesAsync();
        fixture.PresentSessionId = presentSession.Id;
        fixture.LateSessionId = lateSession.Id;
        return fixture;
    }

    private static LectureSession OpenSession(Course course, string topic, DateTime openedAt, DateTime closesAt) => new()
    {
        Course = course,
        Topic = topic,
        StartsAtUtc = openedAt.AddMinutes(-5),
        EndsAtUtc = closesAt.AddHours(1),
        AttendanceState = AttendanceWindowState.Open,
        AttendanceOpenedAtUtc = openedAt,
        AttendanceClosesAtUtc = closesAt
    };

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
