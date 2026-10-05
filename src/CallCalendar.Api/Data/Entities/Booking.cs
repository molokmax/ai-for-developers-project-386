namespace CallCalendar.Api.Data.Entities;

/// <summary>
/// Бронирование слота клиентом.
/// </summary>
public class Booking
{
    public int Id { get; set; }

    public int TimeSlotId { get; set; }

    public TimeSlot? TimeSlot { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
