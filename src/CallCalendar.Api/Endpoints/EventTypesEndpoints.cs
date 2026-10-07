using CallCalendar.Api.Data;
using CallCalendar.Api.Data.Entities;
using CallCalendar.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

// Генерированный контракт из contracts/generated/openapi.json (MinimalOpenAPI)
using Bases = CallCalendar.Api.Contracts.Endpoints;
using Dtos = CallCalendar.Api.Contracts.Contracts;

namespace CallCalendar.Api.Endpoints;

public sealed class EventTypesListEndpoint(AppDbContext db)
    : Bases.EventTypesListEndpointBase
{
    public override async Task<Ok<Dtos.EventType[]>> HandleAsync(CancellationToken cancellationToken)
    {
        var eventTypes = await db.EventTypes
            .AsNoTracking()
            .OrderBy(eventType => eventType.Name)
            .Select(eventType => new Dtos.EventType
            {
                Id = eventType.Id,
                Name = eventType.Name,
                Description = eventType.Description,
                DurationMinutes = eventType.DurationMinutes,
            })
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(eventTypes);
    }
}

public sealed class EventTypesCreateEndpoint(AppDbContext db)
    : Bases.EventTypesCreateEndpointBase
{
    public override async Task<Results<Created<Dtos.EventType>, BadRequest<Dtos.ProblemDetails>>> HandleAsync(
        Dtos.CreateEventTypeRequest request, CancellationToken cancellationToken)
    {
        // Кратность 30 минутам не выражается констрейнтом схемы, проверяется на сервере
        if (!SlotGrid.IsDurationValid(request.DurationMinutes))
        {
            return TypedResults.BadRequest(ApiProblems.InvalidRequest(
                "Длительность должна быть кратна 30 минутам и не меньше 30"));
        }

        var eventType = new EventType
        {
            Name = request.Name,
            Description = request.Description,
            DurationMinutes = request.DurationMinutes,
        };

        db.EventTypes.Add(eventType);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created<Dtos.EventType>(string.Empty, ToDto(eventType));
    }

    private static Dtos.EventType ToDto(EventType eventType) => new()
    {
        Id = eventType.Id,
        Name = eventType.Name,
        Description = eventType.Description,
        DurationMinutes = eventType.DurationMinutes,
    };
}
