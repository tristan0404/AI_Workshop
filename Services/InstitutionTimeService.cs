namespace AI_Workshop.Services;

public sealed class InstitutionTimeService(IConfiguration configuration)
{
    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById(
        configuration["Institution:TimeZoneId"] ?? "Africa/Johannesburg");

    public DateTime ToUtc(DateTime localDateTime) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), _timeZone);

    public DateTime ToLocal(DateTime utcDateTime) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), _timeZone);

    public string TimeZoneLabel => _timeZone.DisplayName;
}
