using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VeriScan.Application.Abstractions;

namespace VeriScan.Api.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ApplicationBaseException applicationException)
        {
            ApiExceptionLog.Unhandled(logger, exception);
            return false;
        }

        var statusCode = applicationException switch
        {
            ResourceNotFoundException => StatusCodes.Status404NotFound,
            RequestConflictException => StatusCodes.Status409Conflict,
            UnsupportedOperationException => StatusCodes.Status422UnprocessableEntity,
            RequestValidationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };

        ApiExceptionLog.Rejected(logger, applicationException.ErrorCode, statusCode);
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Type = $"https://veriscan.invalid/problems/{applicationException.ErrorCode}",
            Title = statusCode switch
            {
                StatusCodes.Status404NotFound => "Resource not found",
                StatusCodes.Status409Conflict => "Conflict",
                StatusCodes.Status422UnprocessableEntity => "Unsupported operation",
                _ => "Request validation failed"
            },
            Status = statusCode,
            Detail = applicationException.Message,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["code"] = applicationException.ErrorCode;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
        return true;
    }
}

internal static partial class ApiExceptionLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "未处理的 API 异常。")]
    public static partial void Unhandled(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "API 请求被业务校验拒绝，错误码：{ErrorCode}，状态码：{StatusCode}。")]
    public static partial void Rejected(ILogger logger, string errorCode, int statusCode);
}
