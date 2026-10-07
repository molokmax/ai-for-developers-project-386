using MinimalOpenAPI;

namespace CallCalendar.Api.Endpoints;

/// <summary>
/// Эндпоинты из контракта: маршруты и метаданные генерируются MinimalOpenAPI из
/// contracts/generated/openapi.json, реализации хендлеров лежат рядом в Endpoints/.
/// Валидация запросов (DataAnnotations из контракта) вешается фильтром на всю группу.
/// </summary>
public static class CallCalendarEndpoints
{
    public static IEndpointRouteBuilder MapCallCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapMinimalOpenApiEndpoints();
        group.AddEndpointFilter<ValidationEndpointFilter>();

        return app;
    }
}
