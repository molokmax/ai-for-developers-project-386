using CallCalendar.Api.Data;
using CallCalendar.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);
}

// HTTPS редирект и терминирование TLS настраиваются на хостинге,
// в dev всё ходит через Vite-прокси на http://localhost:5262.
app.MapHealthEndpoints();
app.MapSlotEndpoints();

app.Run();

/// <summary>
/// Маркер для интеграционных тестов (WebApplicationFactory&lt;Program&gt;).
/// </summary>
public partial class Program;

