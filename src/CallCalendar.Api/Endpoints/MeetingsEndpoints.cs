using CallCalendar.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

// Генерированный контракт из contracts/generated/openapi.json (MinimalOpenAPI)
using Bases = CallCalendar.Api.Contracts.Endpoints;
using Dtos = CallCalendar.Api.Contracts.Contracts;

namespace CallCalendar.Api.Endpoints;

public sealed class MeetingsListEndpoint(AppDbContext db)
    : Bases.MeetingsListEndpointBase
{
    public override async Task<Ok<Dtos.Meeting[]>> HandleAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        var meetings = await db.Bookings
            .AsNoTracking()
            .Where(booking => booking.StartUtc >= nowUtc)
            .OrderBy(booking => booking.StartUtc)
            .Select(booking => new Dtos.Meeting
            {
                Id = booking.Id,
                EventType = new Dtos.MeetingEventType
                {
                    Id = booking.EventType!.Id,
                    Name = booking.EventType.Name,
                },
                StartUtc = new DateTimeOffset(booking.StartUtc, TimeSpan.Zero),
                EndUtc = new DateTimeOffset(booking.EndUtc, TimeSpan.Zero),
                CustomerName = booking.CustomerName,
                CustomerEmail = booking.CustomerEmail,
            })
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(meetings);
    }
}
