using System.Globalization;
using Guardou.Application.Files;
using Guardou.Application.Quotas;
using Guardou.Application.Search;
using Guardou.Application.Settings;
using Guardou.Application.Shares;
using Guardou.Application.Tasks;
using Guardou.Application.Uploads;
using Guardou.Application.Users;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Guardou.Api.Errors;

internal sealed partial class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    private const string CodesExtension = "codes";

    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        int statusCode = StatusCodeOf(exception);
        bool isServerError = statusCode >= StatusCodes.Status500InternalServerError
            && exception is not (InsufficientStorageException or ArchiveBusyException);
        if (isServerError)
        {
            LogUnhandledException(exception, httpContext.Request.Method, RequestPathRedaction.Redact(httpContext.Request.Path));
        }
        else if (exception is InsufficientStorageException)
        {
            LogInsufficientStorage(httpContext.Request.Method, RequestPathRedaction.Redact(httpContext.Request.Path));
        }

        httpContext.Response.StatusCode = statusCode;
        if (exception is ArchiveBusyException)
        {
            httpContext.Response.Headers.RetryAfter = ArchiveBusyException.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        }

        return problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = ProblemOf(exception, statusCode, isServerError),
        });
    }

    private static ProblemDetails ProblemOf(Exception exception, int statusCode, bool isServerError)
    {
        ProblemDetails problem = new() { Status = statusCode, Detail = isServerError ? null : exception.Message };
        if (exception is UserAccountRejectedException rejected)
        {
            problem.Extensions[CodesExtension] = rejected.Errors.Select(error => error.Code).ToList();
        }

        if (exception is UploadInProgressException)
        {
            problem.Extensions[CodesExtension] = new[] { UploadInProgressException.Code };
        }

        if (exception is ItemChangedException)
        {
            problem.Extensions[CodesExtension] = new[] { ItemChangedException.Code };
        }

        if (exception is QuotaExceededException)
        {
            problem.Extensions[CodesExtension] = new[] { QuotaExceededException.Code };
        }

        if (exception is FileTooLargeException)
        {
            problem.Extensions[CodesExtension] = new[] { FileTooLargeException.Code };
        }

        if (exception is InsufficientStorageException)
        {
            problem.Extensions[CodesExtension] = new[] { InsufficientStorageException.Code };
        }

        if (exception is TooManyUploadsException)
        {
            problem.Extensions[CodesExtension] = new[] { TooManyUploadsException.Code };
        }

        if (exception is ArchiveTooLargeException)
        {
            problem.Extensions[CodesExtension] = new[] { ArchiveTooLargeException.Code };
        }

        return problem;
    }

    private static int StatusCodeOf(Exception exception)
    {
        return exception switch
        {
            BadHttpRequestException badRequest => badRequest.StatusCode,
            InvalidItemRequestException => StatusCodes.Status400BadRequest,
            InvalidSearchQueryException => StatusCodes.Status400BadRequest,
            ItemNotFoundException => StatusCodes.Status404NotFound,
            ItemConflictException => StatusCodes.Status409Conflict,
            ItemChangedException => StatusCodes.Status409Conflict,
            ShareNotFoundException => StatusCodes.Status404NotFound,
            UserAccountRejectedException => StatusCodes.Status400BadRequest,
            UserNotFoundException => StatusCodes.Status404NotFound,
            InvalidProfilePictureException => StatusCodes.Status400BadRequest,
            TaskNotFoundException => StatusCodes.Status404NotFound,
            TaskConflictException => StatusCodes.Status409Conflict,
            InvalidSettingsException => StatusCodes.Status400BadRequest,
            UploadNotFoundException => StatusCodes.Status404NotFound,
            UploadInProgressException => StatusCodes.Status409Conflict,
            QuotaExceededException => StatusCodes.Status413PayloadTooLarge,
            FileTooLargeException => StatusCodes.Status413PayloadTooLarge,
            InsufficientStorageException => StatusCodes.Status507InsufficientStorage,
            TooManyUploadsException => StatusCodes.Status429TooManyRequests,
            ArchiveTooLargeException => StatusCodes.Status413PayloadTooLarge,
            ArchiveBusyException => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The request {Method} {Path} was refused: the server is short of free space.")]
    private partial void LogInsufficientStorage(string method, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "The request {Method} {Path} failed.")]
    private partial void LogUnhandledException(Exception exception, string method, string path);
}
