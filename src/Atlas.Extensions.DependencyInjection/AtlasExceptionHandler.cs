using Atlas.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Atlas.Extensions.DependencyInjection;

public sealed class AtlasExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<AtlasExceptionHandler> _logger;

    public AtlasExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<AtlasExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            Atlas.Core.Exceptions.ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            InvalidOperationException => (StatusCodes.Status400BadRequest, "Invalid operation"),
            TenantNotFoundException => (StatusCodes.Status404NotFound, "Tenant not found"),
            AtlasException => (StatusCodes.Status400BadRequest, "Request failed"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled request exception.");
        }
        else
        {
            _logger.LogWarning(exception, "Request exception handled as {StatusCode}.", statusCode);
        }

        httpContext.Response.StatusCode = statusCode;
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = CreateProblemDetails(httpContext, exception, statusCode, title)
        });
    }

    private static ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        Exception exception,
        int statusCode,
        string title)
    {
        var details = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode >= StatusCodes.Status500InternalServerError
                ? "An unexpected server error occurred."
                : exception.Message,
            Instance = httpContext.Request.Path
        };

        if (exception is Atlas.Core.Exceptions.ValidationException validationException)
            details.Extensions["errors"] = validationException.Errors;

        details.Extensions["traceId"] = httpContext.TraceIdentifier;
        return details;
    }
}
