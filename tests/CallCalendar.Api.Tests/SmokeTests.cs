using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CallCalendar.Api.Tests;

/// <summary>
/// Smoke-тесты контракта: приложение стартует, применяет миграции, сидирует типы событий
/// и обслуживает операции API по OpenAPI-контракту из contracts/generated/openapi.json.
/// </summary>
public class SmokeTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private static readonly string[] SeededEventTypeNames =
    [
        "Вводный звонок",
        "Обсуждение проекта",
        "Ревью кода",
    ];

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EventTypes_ReturnsSeededCatalog()
    {
        var client = factory.CreateClient();

        var document = JsonDocument.Parse(await client.GetStringAsync("/api/event-types"));

        var names = document.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("name").GetString()!)
            .ToArray();

        Assert.NotEmpty(names);
        Assert.Subset(SeededEventTypeNames.ToHashSet(), names.ToHashSet());
    }

    [Fact]
    public async Task EventTypesCreate_RejectsInvalidDuration()
    {
        var client = factory.CreateClient();

        // 25: не проходит Range(30, ...) - 400 от валидационного фильтра (application/problem+json)
        var belowRange = await client.PostAsJsonAsync("/api/event-types", new
        {
            name = "Обсуждение",
            description = "Описание",
            durationMinutes = 25,
        });

        Assert.Equal(HttpStatusCode.BadRequest, belowRange.StatusCode);
        Assert.Equal("application/problem+json", belowRange.Content.Headers.ContentType?.MediaType);
        Assert.True(JsonDocument.Parse(await belowRange.Content.ReadAsStringAsync())
            .RootElement.TryGetProperty("errors", out _));

        // 50: проходит Range, но не кратна 30 - 400 от хендлера (application/json, как в контракте)
        var notMultiple = await client.PostAsJsonAsync("/api/event-types", new
        {
            name = "Обсуждение",
            description = "Описание",
            durationMinutes = 50,
        });

        Assert.Equal(HttpStatusCode.BadRequest, notMultiple.StatusCode);
        Assert.Equal("application/json", notMultiple.Content.Headers.ContentType?.MediaType);

        var problem = JsonDocument.Parse(await notMultiple.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.NotNull(problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task EventTypesCreate_CreatesTypeWithLocationAbsent()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/event-types", new
        {
            name = "Быстрый созвон",
            description = "Короткое обсуждение",
            durationMinutes = 30,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(string.IsNullOrEmpty(response.Headers.Location?.ToString()),
            "Контракт EventTypes_create не объявляет заголовок Location");
    }

    [Fact]
    public async Task Slots_ComputedOnThirtyMinuteGridInBookingWindow()
    {
        var client = factory.CreateClient();
        var (eventTypeId, durationMinutes) = await GetFirstEventTypeAsync(client);

        var slots = await GetSlotsAsync(client, eventTypeId);

        Assert.NotEmpty(slots);
        foreach (var slot in slots)
        {
            var start = DateTimeOffset.Parse(slot.GetProperty("startUtc").GetString()!, CultureInfo.InvariantCulture);
            var end = DateTimeOffset.Parse(slot.GetProperty("endUtc").GetString()!, CultureInfo.InvariantCulture);

            Assert.Equal(TimeSpan.FromMinutes(durationMinutes), end - start);
            Assert.Equal(0, start.Minute % 30);
            Assert.Equal(0, start.Second);
        }

        // Прошедшие слоты отсечены
        Assert.All(slots, slot => Assert.True(
            DateTimeOffset.Parse(slot.GetProperty("startUtc").GetString()!, CultureInfo.InvariantCulture) >=
            DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    [Fact]
    public async Task Slots_UnknownEventType_ReturnsNotFound()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/slots?eventTypeId=999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Booking_CreatesRecordWithLocationHeader()
    {
        var client = factory.CreateClient();
        var (eventTypeId, _, slotStart) = await GetBookableSlotAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/bookings");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new
        {
            eventTypeId,
            startUtc = slotStart,
            customerName = "Мария Иванова",
            customerEmail = "maria@example.com",
        });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("/api/bookings/", response.Headers.Location?.ToString());

        var booking = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(eventTypeId, booking.GetProperty("eventType").GetProperty("id").GetInt64());
        Assert.Equal(slotStart, booking.GetProperty("startUtc").GetString());
        Assert.Equal("Мария Иванова", booking.GetProperty("customerName").GetString());
    }

    [Fact]
    public async Task Booking_RepeatWithSameIdempotencyKey_ReturnsPrimaryResult()
    {
        var client = factory.CreateClient();
        var (eventTypeId, _, slotStart) = await GetBookableSlotAsync(client);
        var idempotencyKey = Guid.NewGuid().ToString();

        var first = await PostBookingAsync(client, idempotencyKey, eventTypeId, slotStart);
        var second = await PostBookingAsync(client, idempotencyKey, eventTypeId, slotStart);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(first.Headers.Location, second.Headers.Location);

        var firstId = JsonDocument.Parse(await first.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt64();
        var secondId = JsonDocument.Parse(await second.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt64();
        Assert.Equal(firstId, secondId);
    }

    [Fact]
    public async Task Booking_SecondGuestOnSameInterval_ReturnsConflict()
    {
        var client = factory.CreateClient();
        var (eventTypeId, _, slotStart) = await GetBookableSlotAsync(client);

        var first = await PostBookingAsync(client, Guid.NewGuid().ToString(), eventTypeId, slotStart);
        var second = await PostBookingAsync(client, Guid.NewGuid().ToString(), eventTypeId, slotStart);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/json", second.Content.Headers.ContentType?.MediaType);

        var problem = JsonDocument.Parse(await second.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Booking_InvalidEmail_ReturnsBadRequest()
    {
        var client = factory.CreateClient();
        var (eventTypeId, _, slotStart) = await GetBookableSlotAsync(client);

        var response = await PostBookingAsync(
            client, Guid.NewGuid().ToString(), eventTypeId, slotStart, customerEmail: "не-почта");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Booking_UnknownEventType_ReturnsNotFound()
    {
        var client = factory.CreateClient();
        var (_, _, slotStart) = await GetBookableSlotAsync(client);

        var response = await PostBookingAsync(client, Guid.NewGuid().ToString(), 999999, slotStart);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Booking_OffGridStart_ReturnsBadRequest()
    {
        var client = factory.CreateClient();
        var (eventTypeId, _, slotStart) = await GetBookableSlotAsync(client);

        var offGrid = DateTimeOffset.Parse(slotStart, CultureInfo.InvariantCulture)
            .AddMinutes(10)
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        var response = await PostBookingAsync(client, Guid.NewGuid().ToString(), eventTypeId, offGrid);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Booking_DifferentEventTypesOnSameInterval_ReturnsConflict()
    {
        var client = factory.CreateClient();
        var eventTypes = await GetEventTypesAsync(client);
        var first = eventTypes[0];
        var second = eventTypes.First(type => type.Id != first.Id);

        // Слот 09:00 по времени владельца: полный рабочий день, свободен у обоих типов
        var morningStart = await GetMorningSlotStartAsync(client, first.Id);

        var firstBooking = await PostBookingAsync(client, Guid.NewGuid().ToString(), first.Id, morningStart);
        Assert.Equal(HttpStatusCode.Created, firstBooking.StatusCode);

        var secondBooking = await PostBookingAsync(client, Guid.NewGuid().ToString(), second.Id, morningStart);

        Assert.Equal(HttpStatusCode.Conflict, secondBooking.StatusCode);
    }

    [Fact]
    public async Task Booking_LastWindowDayIsBookable_DayBeyondWindowIsRejected()
    {
        var client = factory.CreateClient();
        var (eventTypeId, _) = await GetFirstEventTypeAsync(client);

        var slots = await GetSlotsAsync(client, eventTypeId);
        // Последний свободный слот лежит на 14-м дне окна (включая текущий)
        var lastStart = slots
            .Select(slot => DateTimeOffset.Parse(slot.GetProperty("startUtc").GetString()!, CultureInfo.InvariantCulture))
            .Max();

        var lastDay = await PostBookingAsync(
            client, Guid.NewGuid().ToString(), eventTypeId, FormatUtc(lastStart));
        Assert.Equal(HttpStatusCode.Created, lastDay.StatusCode);

        var beyondWindow = await PostBookingAsync(
            client, Guid.NewGuid().ToString(), eventTypeId, FormatUtc(lastStart.AddDays(1)));
        Assert.Equal(HttpStatusCode.BadRequest, beyondWindow.StatusCode);
    }

    [Fact]
    public async Task Booking_OutsideWorkingHours_ReturnsBadRequest()
    {
        var client = factory.CreateClient();
        var eventTypes = await GetEventTypesAsync(client);
        var halfHourType = eventTypes.First(type => type.DurationMinutes == 30);
        var longType = eventTypes.Where(type => type.DurationMinutes > 30).Select(type => type.Id).Cast<long?>().FirstOrDefault();

        // 08:30 на сетке, но до начала рабочего дня 09:00
        var beforeWork = await PostBookingAsync(
            client, Guid.NewGuid().ToString(), halfHourType.Id, LocalStartUtc(2, 8, 30));
        Assert.Equal(HttpStatusCode.BadRequest, beforeWork.StatusCode);

        // 18:00 на сетке, но это конец рабочего дня: интервал не помещается
        var afterWork = await PostBookingAsync(
            client, Guid.NewGuid().ToString(), halfHourType.Id, LocalStartUtc(2, 18, 0));
        Assert.Equal(HttpStatusCode.BadRequest, afterWork.StatusCode);

        // Старт 17:30 при длительности больше 30 минут выходит за 18:00
        if (longType is not null)
        {
            var exceeding = await PostBookingAsync(
                client, Guid.NewGuid().ToString(), longType.Value, LocalStartUtc(2, 17, 30));
            Assert.Equal(HttpStatusCode.BadRequest, exceeding.StatusCode);
        }
    }

    [Fact]
    public async Task Meetings_ReturnsUpcomingBookingsSortedByStart()
    {
        var client = factory.CreateClient();
        var (eventTypeId, _, slotStart) = await GetBookableSlotAsync(client);
        await PostBookingAsync(client, Guid.NewGuid().ToString(), eventTypeId, slotStart);

        var document = JsonDocument.Parse(await client.GetStringAsync("/api/meetings"));

        var starts = document.RootElement.EnumerateArray()
            .Select(item => DateTimeOffset.Parse(item.GetProperty("startUtc").GetString()!, CultureInfo.InvariantCulture))
            .ToArray();

        Assert.NotEmpty(starts);
        Assert.Equal(starts.OrderBy(s => s), starts);
        Assert.All(starts, start => Assert.True(start >= DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    [Fact]
    public async Task AuthoredSchema_IsPublishedInDevelopment()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/schema.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task RuntimeOpenApiEndpoint_IsRemoved()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<(long EventTypeId, int DurationMinutes)> GetFirstEventTypeAsync(HttpClient client)
    {
        var document = JsonDocument.Parse(await client.GetStringAsync("/api/event-types"));
        var first = document.RootElement.EnumerateArray().First();

        return (first.GetProperty("id").GetInt64(), first.GetProperty("durationMinutes").GetInt32());
    }

    private static async Task<(long Id, int DurationMinutes)[]> GetEventTypesAsync(HttpClient client)
    {
        var document = JsonDocument.Parse(await client.GetStringAsync("/api/event-types"));

        return document.RootElement.EnumerateArray()
            .Select(item => (
                Id: item.GetProperty("id").GetInt64(),
                DurationMinutes: item.GetProperty("durationMinutes").GetInt32()))
            .ToArray();
    }

    private static async Task<string> GetMorningSlotStartAsync(HttpClient client, long eventTypeId)
    {
        var slots = await GetSlotsAsync(client, eventTypeId);

        var morningStart = slots
            .Select(slot => (
                Raw: slot.GetProperty("startUtc").GetString()!,
                Start: DateTimeOffset.Parse(slot.GetProperty("startUtc").GetString()!, CultureInfo.InvariantCulture)))
            .FirstOrDefault(slot =>
                TimeZoneInfo.ConvertTime(slot.Start, OwnerTimeZone).TimeOfDay == TimeSpan.FromHours(9));

        Assert.NotEqual(default, morningStart.Start);
        return morningStart.Raw;
    }

    // Часовой пояс владельца: Calendar:TimeZone из appsettings.json
    private static readonly TimeZoneInfo OwnerTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");

    private static string LocalStartUtc(int dayOffset, int hour, int minute)
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, OwnerTimeZone).Date
            .AddDays(dayOffset)
            .AddHours(hour)
            .AddMinutes(minute);

        return TimeZoneInfo.ConvertTimeToUtc(local, OwnerTimeZone)
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    private static string FormatUtc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static async Task<JsonElement[]> GetSlotsAsync(HttpClient client, long eventTypeId)
    {
        var document = JsonDocument.Parse(await client.GetStringAsync($"/api/slots?eventTypeId={eventTypeId}"));

        return document.RootElement.EnumerateArray().ToArray();
    }

    private static async Task<(long EventTypeId, int DurationMinutes, string StartUtc)> GetBookableSlotAsync(HttpClient client)
    {
        var (eventTypeId, durationMinutes) = await GetFirstEventTypeAsync(client);
        var slots = await GetSlotsAsync(client, eventTypeId);

        Assert.True(slots.Length > 0, "Сидированные типы событий должны давать свободные слоты в окне записи");

        return (eventTypeId, durationMinutes, slots[0].GetProperty("startUtc").GetString()!);
    }

    private static async Task<HttpResponseMessage> PostBookingAsync(
        HttpClient client,
        string idempotencyKey,
        long eventTypeId,
        string startUtc,
        string customerName = "Мария Иванова",
        string customerEmail = "maria@example.com")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/bookings");
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = JsonContent.Create(new
        {
            eventTypeId,
            startUtc,
            customerName,
            customerEmail,
        });

        var response = await client.SendAsync(request);

        return response;
    }
}

/// <summary>
/// Runs the app in-memory on a temporary SQLite file, so tests don't
/// touch the developer's callcalendar.db.
/// </summary>
public sealed class WebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"callcalendar-smoke-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                try
                {
                    File.Delete(_dbPath + suffix);
                }
                catch (IOException)
                {
                    // The temp file may linger for a short while - not critical for the smoke test
                }
            }
        }
    }
}
