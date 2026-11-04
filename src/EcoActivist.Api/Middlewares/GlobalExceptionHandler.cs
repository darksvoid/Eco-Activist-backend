using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EcoActivist.Api.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Произошла необработанная ошибка: {Message}", exception.Message);

        var (statusCode, title, detail) = exception switch
        {
            InvalidOperationException ex => (StatusCodes.Status400BadRequest, "Недопустимая операция", ex.Message),
            ArgumentException ex => (StatusCodes.Status400BadRequest, "Неверный аргумент", ex.Message),
            KeyNotFoundException ex => (StatusCodes.Status404NotFound, "Ресурс не найден", ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "Внутренняя ошибка сервера", "Произошла непредвиденная ошибка на сервере.")
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
