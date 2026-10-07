using CallCalendar.Api.Data;
using CallCalendar.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

// Генерированный контракт из contracts/generated/openapi.json (MinimalOpenAPI)
using Bases = CallCalendar.Api.Contracts.Endpoints;
using Dtos = CallCalendar.Api.Contracts.Contracts;

namespace CallCalendar.Api.Endpoints;

public sealed class SlotsListEndpoint(AppDbContext db, CalendarOptions calendar)
    : Bases.SlotsListEndpointBase
{
    public override async Task<Results<Ok<Dtos.Slot[]>, NotFound<Dtos.ProblemDetails>>> HandleAsync(
        Bases.SlotsListEndpointBase.Parameters parameters, CancellationToken cancellationToken)
    {
        var eventType = await db.EventTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(eventType => eventType.Id == parameters.EventTypeId, cancellationToken);

        if (eventType is null)
        {
            return TypedResults.NotFound(ApiProblems.EventTypeNotFound(parameters.EventTypeId));
        }

        var nowUtc = DateTime.UtcNow;
        var duration = TimeSpan.FromMinutes(eventType.DurationMinutes);

        // Занятость в пределах окна записи: пересечения с записями любых типов событий
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, calendar.TimeZone).Date);
        var windowStart = ToUtc(todayLocal.ToDateTime(SlotGrid.WorkStart), calendar.TimeZone);
        var windowEnd = ToUtc(todayLocal.AddDays(SlotGrid.BookingWindowDays).ToDateTime(SlotGrid.WorkEnd), calendar.TimeZone);

        var busy = await db.Bookings
            .AsNoTracking()
            .Where(booking => booking.StartUtc < windowEnd && booking.EndUtc > windowStart)
            .Select(booking => new ValueTuple<DateTime, DateTime>(booking.StartUtc, booking.EndUtc))
            .ToArrayAsync(cancellationToken);

        var slots = SlotGrid
            .FreeSlots(calendar.TimeZone, duration, nowUtc, busy)
            .Select(slot => new Dtos.Slot
            {
                StartUtc = new DateTimeOffset(slot.Item1, TimeSpan.Zero),
                EndUtc = new DateTimeOffset(slot.Item2, TimeSpan.Zero),
            })
            .ToArray();

        return TypedResults.Ok(slots);
    }

    private static DateTime ToUtc(DateTime localDateTime, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);
}
