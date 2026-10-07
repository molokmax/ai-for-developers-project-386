namespace CallCalendar.Api.Services;

/// <summary>
/// Настройки календаря владельца: часовой пояс для сетки слотов и окна записи.
/// </summary>
public sealed class CalendarOptions(TimeZoneInfo timeZone)
{
    public const string ConfigurationKey = "Calendar:TimeZone";

    public const string DefaultTimeZoneId = "Europe/Moscow";

    public TimeZoneInfo TimeZone { get; } = timeZone;

    public static CalendarOptions FromConfiguration(IConfiguration configuration)
    {
        var timeZoneId = configuration[ConfigurationKey] ?? DefaultTimeZoneId;
        return new CalendarOptions(TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
    }
}
