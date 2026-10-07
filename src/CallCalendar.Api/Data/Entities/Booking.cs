namespace CallCalendar.Api.Data.Entities;

/// <summary>
/// Запись: занятый интервал с контактами гостя. Слоты не хранятся, хранятся только записи.
/// Все времена в UTC (DateTime.Kind = Utc, обеспечивается конвертером при материализации).
/// </summary>
public class Booking
{
    public long Id { get; set; }

    public long EventTypeId { get; set; }

    public EventType? EventType { get; set; }

    public DateTime StartUtc { get; set; }

    /// <summary>Конец интервала: начало плюс длительность типа события.</summary>
    public DateTime EndUtc { get; set; }

    public required string CustomerName { get; set; }

    public required string CustomerEmail { get; set; }

    /// <summary>Ключ идемпотентности (UUID из заголовка Idempotency-Key).</summary>
    public required string IdempotencyKey { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
