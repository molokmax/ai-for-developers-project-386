using CallCalendar.Api.Data.Entities;

namespace CallCalendar.Api.Data;

/// <summary>
/// Демо-данные для локальной разработки: часовые слоты на несколько дней вперёд.
/// </summary>
public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.TimeSlots.Any())
        {
            return;
        }

        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var slots = new List<TimeSlot>();

        for (var day = 0; day < 3; day++)
        {
            for (var hour = 9; hour < 18; hour++)
            {
                slots.Add(new TimeSlot
                {
                    StartUtc = start.AddDays(day).ToDateTime(new TimeOnly(hour, 0)),
                    Duration = TimeSpan.FromHours(1),
                });
            }
        }

        db.TimeSlots.AddRange(slots);
        db.SaveChanges();
    }
}
