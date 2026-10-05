using System.Net;
using System.Net.Http.Json;
using CallCalendar.Api.Endpoints;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CallCalendar.Api.Tests;

/// <summary>
/// Dymovoy (smoke) test: the application starts, applies migrations,
/// seeds demo data, and responds over HTTP.
/// </summary>
public class SmokeTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Slots_ReturnsSeededSlots()
    {
        var client = factory.CreateClient();

        var slots = await client.GetFromJsonAsync<List<SlotDto>>("/api/slots");

        Assert.NotNull(slots);
        Assert.NotEmpty(slots);
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
