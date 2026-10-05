using CallCalendar.Api.Data;
using CallCalendar.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CallCalendar.Api.Endpoints;

public static class SlotEndpoints
{
    public static IEndpointRouteBuilder MapSlotEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/slots", async (AppDbContext db, DateTime? from, DateTime? to, CancellationToken ct) =>
        {
            var query = db.TimeSlots.AsNoTracking();

            if (from is not null)
            {
                query = query.Where(slot => slot.StartUtc >= from);
            }

            if (to is not null)
            {
                query = query.Where(slot => slot.StartUtc <= to);
            }

            var slots = await query
                .OrderBy(slot => slot.StartUtc)
                .Select(slot => new SlotDto(slot.Id, slot.StartUtc, slot.Duration, slot.IsBooked))
                .ToListAsync(ct);

            return Results.Ok(slots);
        })
        .WithName("GetSlots")
        .WithSummary("Список слотов календаря");

        // Каркас: сохранение без проверок. Бизнес-правила (занятость слота,
        // валидация данных клиента и т.п.) добавляются поверх этого.
        api.MapPost("/bookings", async (AppDbContext db, CreateBookingRequest request, CancellationToken ct) =>
        {
            var slot = await db.TimeSlots.FirstOrDefaultAsync(s => s.Id == request.SlotId, ct);

            if (slot is null)
            {
                return Results.NotFound();
            }

            var booking = new Booking
            {
                TimeSlotId = slot.Id,
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
            };

            db.Bookings.Add(booking);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/bookings/{booking.Id}", booking.Id);
        })
        .WithName("CreateBooking")
        .WithSummary("Создание бронирования (каркас, без бизнес-правил)");

        return app;
    }
}

public record SlotDto(int Id, DateTime StartUtc, TimeSpan Duration, bool IsBooked);

public record CreateBookingRequest(int SlotId, string CustomerName, string CustomerEmail);
