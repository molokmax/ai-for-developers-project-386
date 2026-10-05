namespace CallCalendar.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
            .WithName("Health")
            .WithSummary("Проверка живости сервиса");

        return app;
    }
}
