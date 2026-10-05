namespace CallCalendar.Api.Data.Entities;

/// <summary>
/// Слот календаря, доступный для бронирования.
/// </summary>
public class TimeSlot
{
    public int Id { get; set; }

    public DateTime StartUtc { get; set; }

    public TimeSpan Duration { get; set; }

    public bool IsBooked { get; set; }

    public List<Booking> Bookings { get; set; } = [];
}
