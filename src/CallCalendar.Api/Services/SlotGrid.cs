namespace CallCalendar.Api.Services;

/// <summary>
/// Сетка слотов: каждые 30 минут в интервале 09:00-18:00 каждого дня по времени владельца,
/// окно записи 14 календарных дней, включая текущий. Слот свободен, если его интервал
/// не пересекается ни с одной существующей записью. Все времена в UTC (DateTime.Kind = Utc).
/// </summary>
public static class SlotGrid
{
    public const int BookingWindowDays = 14;

    public const int GridStepMinutes = 30;

    public static readonly TimeOnly WorkStart = new(9, 0);

    public static readonly TimeOnly WorkEnd = new(18, 0);

    /// <summary>Длительность допустима, если кратна 30 и минимум 30 минут.</summary>
    public static bool IsDurationValid(int minutes) => minutes >= GridStepMinutes && minutes % GridStepMinutes == 0;

    /// <summary>
    /// Свободные слоты для длительности <paramref name="duration"/> в окне записи:
    /// старта на сетке, интервал целиком в рабочем дне, не в прошлом, без пересечений с <paramref name="busy"/>.
    /// </summary>
    public static IEnumerable<(DateTime StartUtc, DateTime EndUtc)> FreeSlots(
        TimeZoneInfo timeZone,
        TimeSpan duration,
        DateTime nowUtc,
        IReadOnlyList<(DateTime StartUtc, DateTime EndUtc)> busy)
    {
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, timeZone).Date);

        for (var day = 0; day < BookingWindowDays; day++)
        {
            var workStartUtc = ToUtc(todayLocal.AddDays(day).ToDateTime(WorkStart), timeZone);
            var workEndUtc = ToUtc(todayLocal.AddDays(day).ToDateTime(WorkEnd), timeZone);

            for (var start = workStartUtc; start + duration <= workEndUtc; start += TimeSpan.FromMinutes(GridStepMinutes))
            {
                if (start < nowUtc)
                {
                    continue;
                }

                var end = start + duration;
                if (busy.Any(slot => slot.StartUtc < end && slot.EndUtc > start))
                {
                    continue;
                }

                yield return (start, end);
            }
        }
    }

    /// <summary>
    /// Проверка, что запрошенное начало записи лежит на сетке слотов: кратность 30 минутам,
    /// интервал целиком в рабочем дне (09:00-18:00), внутри окна записи, время не прошло.
    /// </summary>
    public static bool IsStartAvailable(
        TimeZoneInfo timeZone,
        DateTime startUtc,
        TimeSpan duration,
        DateTime nowUtc,
        out string error)
    {
        var local = TimeZoneInfo.ConvertTime(startUtc, timeZone);

        if (local.Minute % GridStepMinutes != 0 || local.Second != 0 || local.Millisecond != 0)
        {
            error = "Начало записи должно лежать на сетке слотов с шагом 30 минут по времени владельца";
            return false;
        }

        if (local.TimeOfDay < WorkStart.ToTimeSpan() || local.TimeOfDay + duration > WorkEnd.ToTimeSpan())
        {
            error = $"Интервал записи должен целиком помещаться в рабочий день {WorkStart:hh\\:mm}-{WorkEnd:hh\\:mm} по времени владельца";
            return false;
        }

        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, timeZone).Date);
        var localDate = DateOnly.FromDateTime(local.Date);
        if (localDate < todayLocal || localDate > todayLocal.AddDays(BookingWindowDays - 1))
        {
            error = $"Запись доступна только в окне {BookingWindowDays} календарных дней по времени владельца, включая текущий день";
            return false;
        }

        if (startUtc < nowUtc)
        {
            error = "Прошедшее время недоступно, включая прошедшие слоты текущего дня";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static DateTime ToUtc(DateTime localDateTime, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);
}
