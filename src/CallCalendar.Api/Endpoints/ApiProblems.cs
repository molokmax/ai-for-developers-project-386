
// Контрактные DTO генерируются из contracts/generated/openapi.json в namespace Contracts.Contracts
using Dtos = CallCalendar.Api.Contracts.Contracts;

namespace CallCalendar.Api.Endpoints;

/// <summary>Типизированные ProblemDetails для контрактных ответов 400/404/409.</summary>
internal static class ApiProblems
{
    public static Dtos.ProblemDetails InvalidRequest(string detail) => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Некорректный запрос",
        Detail = detail,
    };

    public static Dtos.ProblemDetails EventTypeNotFound(long eventTypeId) => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Тип события не найден",
        Detail = $"Тип события {eventTypeId} не найден",
    };

    public static Dtos.ProblemDetails SlotConflict() => new()
    {
        Status = StatusCodes.Status409Conflict,
        Title = "Конфликт занятости",
        Detail = "Интервал записи пересекается с существующей записью",
    };
}
