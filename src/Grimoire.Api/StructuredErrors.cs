using Grimoire.Api.Endpoints;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grimoire.Api;

internal static class StructuredErrors
{
    private const string BodyFault =
        "The request body is missing, malformed, or does not match the expected schema.";

    private const string ParameterFault =
        "A route or query parameter of this request is missing or malformed.";

    public static void UseStructuredErrors(this WebApplication app)
    {
        var logger = app.Services.GetService<ILoggerFactory>()?.CreateLogger(nameof(StructuredErrors));
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
                if (context.Response.StatusCode == StatusCodes.Status400BadRequest && !context.Response.HasStarted)
                {
                    await WriteAsync(
                        context,
                        StatusCodes.Status400BadRequest,
                        "invalid_request",
                        AcceptsBody(context.Request.Method) ? BodyFault : ParameterFault);
                }
            }
            catch (UpstreamUnavailableException ex) when (!context.Response.HasStarted)
            {
                await WriteAsync(
                    context,
                    StatusCodes.Status503ServiceUnavailable,
                    "upstream_unavailable",
                    $"Upstream '{ex.Upstream}' is unavailable: not reachable, it timed out, or it rejected the configured credentials. " +
                    "Nothing was written by this call. Check the service and its configuration, then retry.");
            }
            catch (ReindexRequiredException ex) when (!context.Response.HasStarted)
            {
                await WriteAsync(
                    context,
                    StatusCodes.Status409Conflict,
                    "reindex_required",
                    ex.Message);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // A client abort belongs to Kestrel's shutdown, not to the error contract.
                throw;
            }
            catch (Exception ex) when (!context.Response.HasStarted)
            {
                // The correlation id in the body is the join key to the logged exception;
                // internals never travel to the client. Past HasStarted a write would append
                // to a committed body — let those fall through to the server instead.
                logger?.LogError(ex, "Unhandled exception on {Method} {Path} (correlation {Correlation})",
                    context.Request.Method, context.Request.Path, context.TraceIdentifier);
                await WriteAsync(
                    context,
                    StatusCodes.Status500InternalServerError,
                    "internal_error",
                    $"An unexpected internal failure occurred. Correlation id: {context.TraceIdentifier}.");
            }
        });
    }

    private static bool AcceptsBody(string method) =>
        !(HttpMethods.IsGet(method)
            || HttpMethods.IsHead(method)
            || HttpMethods.IsDelete(method)
            || HttpMethods.IsOptions(method)
            || HttpMethods.IsTrace(method));

    private static async Task WriteAsync(HttpContext context, int statusCode, string code, string message)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(ApiResponses.Payload(code, message), context.RequestAborted);
    }
}
