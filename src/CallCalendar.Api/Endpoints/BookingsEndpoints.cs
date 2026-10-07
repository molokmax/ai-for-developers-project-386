using CallCalendar.Api.Data;
using CallCalendar.Api.Data.Entities;
using CallCalendar.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

// Генерированный контракт из contracts/generated/openapi.json (MinimalOpenAPI)
using Bases = CallCalendar.Api.Contracts.Endpoints;
using Dtos = CallCalendar.Api.Contracts.Contracts;

namespace CallCalendar.Api.Endpoints;

public sealed class BookingsCreateEndpoint(AppDbContext db, CalendarOptions calendar)
    : Bases.BookingsCreateEndpointBase
{
    public override async Task<
        Results<Created<Dtos.Booking>,
            BadRequest<Dtos.ProblemDetails>,
            NotFound<Dtos.ProblemDetails>,
            Conflict<Dtos.ProblemDetails>>> HandleAsync(
        Bases.BookingsCreateEndpointBase.Parameters parameters,
        Dtos.CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        // Идемпотентность: повтор с тем же ключом возвращает первичный результат, дубль не создаётся
        if (string.IsNullOrWhiteSpace(parameters.IdempotencyKey))
        {
            return TypedResults.BadRequest(ApiProblems.InvalidRequest(
                "Заголовок Idempotency-Key обязателен: UUID, генерируемый клиентом на попытку записи"));
        }

        var existing = await db.Bookings
            .AsNoTracking()
            .Include(booking => booking.EventType)
            .FirstOrDefaultAsync(booking => booking.IdempotencyKey == parameters.IdempotencyKey, cancellationToken);

        if (existing is not null)
        {
            return TypedResults.Created(BookingLocation(existing.Id), ToDto(existing));
        }

        var eventType = await db.EventTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(eventType => eventType.Id == request.EventTypeId, cancellationToken);

        if (eventType is null)
        {
            return TypedResults.NotFound(ApiProblems.EventTypeNotFound(request.EventTypeId));
        }

        var nowUtc = DateTime.UtcNow;
        var duration = TimeSpan.FromMinutes(eventType.DurationMinutes);
        var startUtc = request.StartUtc.ToUniversalTime().UtcDateTime;

        if (!SlotGrid.IsStartAvailable(calendar.TimeZone, startUtc, duration, nowUtc, out var error))
        {
            return TypedResults.BadRequest(ApiProblems.InvalidRequest(error));
        }

        var endUtc = startUtc + duration;

        var hasOverlap = await db.Bookings
            .AsNoTracking()
            .AnyAsync(booking => booking.StartUtc < endUtc && booking.EndUtc > startUtc, cancellationToken);

        if (hasOverlap)
        {
            return TypedResults.Conflict(ApiProblems.SlotConflict());
        }

        var booking = new Booking
        {
            EventTypeId = eventType.Id,
            StartUtc = startUtc,
            EndUtc = endUtc,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            IdempotencyKey = parameters.IdempotencyKey,
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(BookingLocation(booking.Id), ToDto(booking));
    }

    private static string BookingLocation(long id) => $"/api/bookings/{id}";

    private static Dtos.Booking ToDto(Booking booking) => new()
    {
        Id = booking.Id,
        EventType = new Dtos.BookingEventType
        {
            Id = booking.EventTypeId,
            Name = booking.EventType?.Name ?? string.Empty,
        },
        StartUtc = new DateTimeOffset(booking.StartUtc, TimeSpan.Zero),
        EndUtc = new DateTimeOffset(booking.EndUtc, TimeSpan.Zero),
        CustomerName = booking.CustomerName,
        CustomerEmail = booking.CustomerEmail,
    };
}
