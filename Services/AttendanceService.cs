using AI_Workshop.Configuration;
using AI_Workshop.Data;
using AI_Workshop.Models.Academic;
using AI_Workshop.Models.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AI_Workshop.Services;

public enum CheckInResultType { Recorded, AlreadyRecorded, Invalid, NotEnrolled, Closed, Expired }
public sealed record CheckInResult(CheckInResultType Type, AttendanceRecord? Record = null, LectureSession? Session = null);

public sealed class AttendanceService(ApplicationDbContext db, IOptions<AttendanceOptions> options)
{
    public async Task<CheckInResult> RecordAsync(int sessionId, string studentId, AttendanceSource source)
    {
        var session = await db.LectureSessions.Include(item => item.Course).FirstOrDefaultAsync(item => item.Id == sessionId);
        if (session is null) return new(CheckInResultType.Invalid);
        if (session.AttendanceState != AttendanceWindowState.Open) return new(CheckInResultType.Closed, Session: session);
        var now = DateTime.UtcNow;
        if (session.AttendanceClosesAtUtc is null || now > session.AttendanceClosesAtUtc) return new(CheckInResultType.Expired, Session: session);
        if (!await db.Enrollments.AnyAsync(item => item.CourseId == session.CourseId && item.StudentId == studentId)) return new(CheckInResultType.NotEnrolled, Session: session);
        var existing = await db.AttendanceRecords.FirstOrDefaultAsync(item => item.LectureSessionId == sessionId && item.StudentId == studentId);
        if (existing is not null) return new(CheckInResultType.AlreadyRecorded, existing, session);

        var lateBoundary = session.AttendanceOpenedAtUtc!.Value.AddMinutes(options.Value.LateAfterMinutes);
        var record = new AttendanceRecord { LectureSessionId = sessionId, StudentId = studentId, Source = source, Status = now > lateBoundary ? AttendanceStatus.Late : AttendanceStatus.Present, CheckedInAtUtc = now };
        db.AttendanceRecords.Add(record);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            db.Entry(record).State = EntityState.Detached;
            var concurrentRecord = await db.AttendanceRecords.FirstOrDefaultAsync(item => item.LectureSessionId == sessionId && item.StudentId == studentId);
            if (concurrentRecord is not null) return new(CheckInResultType.AlreadyRecorded, concurrentRecord, session);
            throw;
        }
        return new(CheckInResultType.Recorded, record, session);
    }
}
