using AI_Workshop.Data;
using AI_Workshop.Models.Attendance;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Services;

public sealed class AttendanceReviewService(ApplicationDbContext db)
{
    private const int MaximumEvidenceBytes = 2_000_000;
    private static readonly HashSet<string> AllowedEvidenceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/jpeg", "image/png"
    };

    public async Task<int> SubmitAsync(int sessionId, string studentId, AttendanceStatus requestedStatus, string explanation,
        string? evidenceFileName, string? evidenceContentType, Stream? evidenceStream, long evidenceLength, CancellationToken cancellationToken)
    {
        explanation = explanation.Trim();
        if (explanation.Length is < 10 or > 2000) throw new AttendanceReviewException("Provide an explanation between 10 and 2,000 characters.");
        var session = await db.LectureSessions.Include(value => value.AttendanceRecords)
            .SingleOrDefaultAsync(value => value.Id == sessionId && value.Course.Enrollments.Any(link => link.StudentId == studentId), cancellationToken)
            ?? throw new AttendanceReviewException("This lecture was not found in your enrolled courses.");
        if (session.StartsAtUtc > DateTime.UtcNow) throw new AttendanceReviewException("You cannot query a lecture that has not started.");
        if (await db.AttendanceQueries.AnyAsync(value => value.LectureSessionId == sessionId && value.StudentId == studentId &&
            (value.Status == AttendanceQueryStatus.Submitted || value.Status == AttendanceQueryStatus.UnderReview), cancellationToken))
            throw new AttendanceReviewException("You already have an open query for this lecture.");

        byte[]? evidence = null;
        if (evidenceStream is not null && evidenceLength > 0)
        {
            if (evidenceLength > MaximumEvidenceBytes) throw new AttendanceReviewException("Evidence files must be smaller than 2 MB.");
            if (evidenceContentType is null || !AllowedEvidenceTypes.Contains(evidenceContentType))
                throw new AttendanceReviewException("Evidence must be a PDF, JPG, or PNG file.");
            using var memory = new MemoryStream();
            await evidenceStream.CopyToAsync(memory, cancellationToken);
            if (memory.Length > MaximumEvidenceBytes) throw new AttendanceReviewException("Evidence files must be smaller than 2 MB.");
            evidence = memory.ToArray();
        }

        var record = session.AttendanceRecords.SingleOrDefault(value => value.StudentId == studentId);
        var query = new AttendanceQuery
        {
            LectureSessionId = sessionId, AttendanceRecordId = record?.Id, StudentId = studentId,
            RequestedStatus = requestedStatus, Explanation = explanation,
            EvidenceFileName = evidence is null ? null : Path.GetFileName(evidenceFileName),
            EvidenceContentType = evidence is null ? null : evidenceContentType,
            EvidenceContent = evidence
        };
        db.AttendanceQueries.Add(query);
        if (record is not null) record.RequiresReview = true;
        await db.SaveChangesAsync(cancellationToken);
        return query.Id;
    }

    public async Task StartReviewAsync(int queryId, string lecturerId, CancellationToken cancellationToken)
    {
        var query = await FindOwnedQueryAsync(queryId, lecturerId, cancellationToken);
        if (query.Status == AttendanceQueryStatus.Submitted)
        {
            query.Status = AttendanceQueryStatus.UnderReview;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ResolveAsync(int queryId, string lecturerId, bool approve, string response, CancellationToken cancellationToken)
    {
        response = response.Trim();
        if (response.Length is < 5 or > 2000) throw new AttendanceReviewException("Provide a response between 5 and 2,000 characters.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var query = await FindOwnedQueryAsync(queryId, lecturerId, cancellationToken);
        if (query.Status is AttendanceQueryStatus.Approved or AttendanceQueryStatus.Rejected)
            throw new AttendanceReviewException("This query has already been resolved.");

        if (approve)
        {
            var record = query.AttendanceRecord ?? await db.AttendanceRecords.SingleOrDefaultAsync(value =>
                value.LectureSessionId == query.LectureSessionId && value.StudentId == query.StudentId, cancellationToken);
            var previous = record?.Status;
            if (record is null)
            {
                record = new AttendanceRecord
                {
                    LectureSessionId = query.LectureSessionId, StudentId = query.StudentId,
                    Status = query.RequestedStatus, Source = AttendanceSource.Lecturer,
                    CheckedInAtUtc = query.LectureSession.StartsAtUtc
                };
                db.AttendanceRecords.Add(record);
            }
            else
            {
                record.Status = query.RequestedStatus;
                record.RequiresReview = false;
            }
            query.AttendanceRecord = record;
            db.AttendanceChangeLogs.Add(new AttendanceChangeLog
            {
                LectureSessionId = query.LectureSessionId, AttendanceRecord = record, AttendanceQuery = query,
                StudentId = query.StudentId, ChangedByLecturerId = lecturerId, PreviousStatus = previous,
                NewStatus = query.RequestedStatus, Reason = response
            });
            query.Status = AttendanceQueryStatus.Approved;
        }
        else
        {
            query.Status = AttendanceQueryStatus.Rejected;
            if (query.AttendanceRecord is not null) query.AttendanceRecord.RequiresReview = false;
        }
        query.LecturerResponse = response;
        query.ReviewedByLecturerId = lecturerId;
        query.ReviewedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateAttendanceAsync(int sessionId, string studentId, string lecturerId, AttendanceStatus status, string reason, CancellationToken cancellationToken)
    {
        reason = reason.Trim();
        if (reason.Length is < 5 or > 1000) throw new AttendanceReviewException("Provide a reason between 5 and 1,000 characters.");
        var session = await db.LectureSessions.SingleOrDefaultAsync(value => value.Id == sessionId &&
            value.Course.Lecturers.Any(link => link.LecturerId == lecturerId) &&
            value.Course.Enrollments.Any(link => link.StudentId == studentId), cancellationToken)
            ?? throw new AttendanceReviewException("The lecture or enrolled student was not found.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var record = await db.AttendanceRecords.SingleOrDefaultAsync(value => value.LectureSessionId == sessionId && value.StudentId == studentId, cancellationToken);
        var previous = record?.Status;
        if (record is null)
        {
            record = new AttendanceRecord { LectureSessionId = sessionId, StudentId = studentId, Status = status, Source = AttendanceSource.Lecturer, CheckedInAtUtc = session.StartsAtUtc };
            db.AttendanceRecords.Add(record);
        }
        else { record.Status = status; record.Source = AttendanceSource.Lecturer; record.RequiresReview = false; }
        db.AttendanceChangeLogs.Add(new AttendanceChangeLog
        {
            LectureSessionId = sessionId, AttendanceRecord = record, StudentId = studentId,
            ChangedByLecturerId = lecturerId, PreviousStatus = previous, NewStatus = status, Reason = reason
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<AttendanceQuery> FindOwnedQueryAsync(int queryId, string lecturerId, CancellationToken cancellationToken)
    {
        return await db.AttendanceQueries.Include(value => value.AttendanceRecord).Include(value => value.LectureSession)
            .SingleOrDefaultAsync(value => value.Id == queryId && value.LectureSession.Course.Lecturers.Any(link => link.LecturerId == lecturerId), cancellationToken)
            ?? throw new AttendanceReviewException("Attendance query not found.");
    }
}

public sealed class AttendanceReviewException(string message) : Exception(message);
