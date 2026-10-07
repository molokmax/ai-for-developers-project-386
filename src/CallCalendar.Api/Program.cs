using CallCalendar.Api.Data;
using CallCalendar.Api.Endpoints;
using CallCalendar.Api.Services;
using Microsoft.EntityFrameworkCore;
using MinimalOpenAPI;

var builder = WebApplication.CreateBuilder(args);

// Порт слушания из переменной окружения PORT (контейнер). В dev приоритетнее
// ASPNETCORE_URLS из launchSettings.json, поведение dotnet run не меняется.
builder.WebHost.UseUrls($"http://+:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

builder.Services.AddMinimalOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton(CalendarOptions.FromConfiguration(builder.Configuration));

var app = builder.Build();

// SPA-статика клиента публикуется в wwwroot при сборке образа (см. Dockerfile);
// в dev wwwroot отсутствует и мидлвар бездействует
app.UseDefaultFiles();
app.UseStaticFiles(CreateStaticFileOptions());

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || IsMigrationsRequested())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);

    // Авторский контракт отдаётся как статика: /openapi/schema.json
    app.MapOpenApiSchemas();
}

// HTTPS-редирект отключен, чтобы не мешать dev-прокси Vite: TLS не настраивается,
// в dev фронт ходит через Vite-прокси на http://localhost:5262.
app.MapHealthEndpoints();
app.MapCallCalendarEndpoints();

// Неизвестные /api/* отдают пустой 404: SPA-fallback ниже их не перехватывает
app.Map("/api/{**path}", () => Results.NotFound());

// Клиентские маршруты react-router: всё остальное - index.html
app.MapFallbackToFile("index.html", CreateStaticFileOptions());

app.Run();

// RUN_MIGRATIONS=1 включает миграции и сидирование вне окружения Development (контейнер).
static bool IsMigrationsRequested()
{
    var value = Environment.GetEnvironmentVariable("RUN_MIGRATIONS");

    return value is not null
        && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase));
}

// Кэширование статики: хэшированные ассеты Vite кэшируются навсегда, index.html
// всегда перепроверяется, иначе после релиза у клиентов останется старый бандл.
static StaticFileOptions CreateStaticFileOptions() => new()
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path;
        var headers = ctx.Context.Response.Headers;

        if (path.StartsWithSegments("/assets"))
        {
            headers.CacheControl = "public, max-age=31536000, immutable";
        }
        else if (path == "/" || path.StartsWithSegments("/index.html"))
        {
            headers.CacheControl = "no-cache";
        }
    },
};

/// <summary>
/// Точка входа (WebApplicationFactory&lt;Program&gt;).
/// </summary>
public partial class Program;
