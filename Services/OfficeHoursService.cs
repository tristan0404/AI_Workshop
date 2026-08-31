using AI_Workshop.Data;
using AI_Workshop.Models.Academic;
using Microsoft.EntityFrameworkCore;

namespace AI_Workshop.Services;

public sealed class OfficeHoursService(ApplicationDbContext db)
{
    public async Task<int> CreateAsync(string lecturerId, DayOfWeek dayOfWeek, TimeOnly startsAt, TimeOnly endsAt,
        string location, CancellationToken cancellationToken)
    {
        location = location.Trim();
        if (!Enum.IsDefined(dayOfWeek)) throw new OfficeHoursException("Choose a valid day of the week.");
        if (endsAt <= startsAt) throw new OfficeHoursException("Office hours must end after they start.");
        if (endsAt - startsAt > TimeSpan.FromHours(8)) throw new OfficeHoursException("An office-hours slot cannot exceed eight hours.");
        if (location.Length is < 2 or > 200) throw new OfficeHoursException("Provide a room or meeting link between 2 and 200 characters.");

        var overlaps = await db.OfficeHours.AnyAsync(slot => slot.LecturerId == lecturerId && slot.IsActive &&
            slot.DayOfWeek == dayOfWeek && startsAt < slot.EndsAt && endsAt > slot.StartsAt, cancellationToken);
        if (overlaps) throw new OfficeHoursException("This slot overlaps with existing office hours.");

        var officeHour = new OfficeHour
        {
            LecturerId = lecturerId,
            DayOfWeek = dayOfWeek,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Location = location
        };
        db.OfficeHours.Add(officeHour);
        await db.SaveChangesAsync(cancellationToken);
        return officeHour.Id;
    }

    public async Task DeleteAsync(int id, string lecturerId, CancellationToken cancellationToken)
    {
        var officeHour = await db.OfficeHours.SingleOrDefaultAsync(slot => slot.Id == id && slot.LecturerId == lecturerId, cancellationToken)
            ?? throw new OfficeHoursException("Office-hours slot not found.");
        db.OfficeHours.Remove(officeHour);
        await db.SaveChangesAsync(cancellationToken);
    }

    public static DateTime NextOccurrence(DayOfWeek dayOfWeek, TimeOnly startsAt, DateTime localNow)
    {
        var daysAhead = ((int)dayOfWeek - (int)localNow.DayOfWeek + 7) % 7;
        var occurrence = localNow.Date.AddDays(daysAhead).Add(startsAt.ToTimeSpan());
        return occurrence <= localNow ? occurrence.AddDays(7) : occurrence;
    }
}

public sealed class OfficeHoursException(string message) : Exception(message);
