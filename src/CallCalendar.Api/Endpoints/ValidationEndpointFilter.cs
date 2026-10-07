using System.ComponentModel.DataAnnotations;

namespace CallCalendar.Api.Endpoints;

/// <summary>
/// Валидация запросов по DataAnnotations из контрактных DTO: 400 с application/problem+json
/// и ошибками по полям. Вложенные объекты не рекурсируются: графы запросов плоские.
/// </summary>
public sealed class ValidationEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var argument in context.Arguments)
        {
            if (argument is null || argument is CancellationToken)
            {
                continue;
            }

            if (argument.GetType().IsPrimitive)
            {
                continue;
            }

            var failures = new List<ValidationResult>();
            var validationContext = new ValidationContext(argument);
            if (Validator.TryValidateObject(argument, validationContext, failures, validateAllProperties: true))
            {
                continue;
            }

            foreach (var failure in failures)
            {
                var key = failure.MemberNames.FirstOrDefault() ?? string.Empty;
                if (!errors.TryGetValue(key, out var messages))
                {
                    messages = [];
                    errors.Add(key, messages);
                }

                messages.Add(failure.ErrorMessage ?? "Недопустимое значение");
            }
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
        }

        return await next(context);
    }
}
