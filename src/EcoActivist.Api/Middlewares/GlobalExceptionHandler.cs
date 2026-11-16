using EcoActivist.Application.Common.Exceptions;
using EcoActivist.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EcoActivist.Api.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        var (statusCode, title, detail) = exception switch
        {
            RequestValidationException ex => (StatusCodes.Status400BadRequest, "Ошибка валидации", ex.Message),
            NotFoundException ex => (StatusCodes.Status404NotFound, "Ресурс не найден", ex.Message),
            ForbiddenAccessException ex => (StatusCodes.Status403Forbidden, "Доступ запрещён", ex.Message),
            AuthenticationFailedException ex => (StatusCodes.Status401Unauthorized, "Ошибка аутентификации", ex.Message),
            UnauthorizedAccessException ex => (StatusCodes.Status401Unauthorized, "Ошибка аутентификации", ex.Message),
            ConflictException ex => (StatusCodes.Status409Conflict, "Конфликт состояния", ex.Message),
            DomainException ex => (StatusCodes.Status422UnprocessableEntity, "Нарушение бизнес-правила", ex.Message),
            ArgumentException ex => (StatusCodes.Status400BadRequest, "Неверный аргумент", ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "Внутренняя ошибка сервера", "Произошла непредвиденная ошибка на сервере.")
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Необработанная ошибка: {Message}", exception.Message);
        else
            _logger.LogInformation("Запрос отклонён ({StatusCode}): {Message}", statusCode, exception.Message);

        ProblemDetails problemDetails = exception is RequestValidationException validation
            ? new ValidationProblemDetails(validation.Errors.ToDictionary(kv => kv.Key, kv => kv.Value))
            : new ProblemDetails();

        problemDetails.Status = statusCode;
        problemDetails.Title = title;
        problemDetails.Detail = detail;
        problemDetails.Instance = httpContext.Request.Path;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = statusCode;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }
}
