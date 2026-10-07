using CallCalendar.Api.Data;
using CallCalendar.Api.Endpoints;
using CallCalendar.Api.Services;
using Microsoft.EntityFrameworkCore;
using MinimalOpenAPI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMinimalOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton(CalendarOptions.FromConfiguration(builder.Configuration));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
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

app.Run();

/// <summary>
/// Точка входа (WebApplicationFactory&lt;Program&gt;).
/// </summary>
public partial class Program;
