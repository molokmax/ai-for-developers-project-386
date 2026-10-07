namespace CallCalendar.Api.Data.Entities;

/// <summary>
/// Тип события: вид встречи, которую владелец календаря предлагает для записи.
/// </summary>
public class EventType
{
    public long Id { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    /// <summary>Длительность в минутах, кратна 30, минимум 30.</summary>
    public int DurationMinutes { get; set; }

    public List<Booking> Bookings { get; set; } = [];
}
