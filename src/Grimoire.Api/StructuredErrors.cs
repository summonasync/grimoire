using Grimoire.Api.Endpoints;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api;

internal static class StructuredErrors
{
    public static void UseStructuredErrors(this WebApplication app)
    {
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
                        "The request body is missing, malformed, or does not match the expected schema.");
                }
            }
            catch (UpstreamUnavailableException ex)
            {
                await WriteAsync(
                    context,
                    StatusCodes.Status503ServiceUnavailable,
                    "upstream_unavailable",
                    $"Upstream '{ex.Upstream}' is unavailable: not reachable, it timed out, or it rejected the configured credentials. " +
                    "Nothing was written by this call. Check the service and its configuration, then retry.");
            }
            catch (ReindexRequiredException ex)
            {
                await WriteAsync(
                    context,
                    StatusCodes.Status409Conflict,
                    "reindex_required",
                    ex.Message);
            }
        });
    }

    private static async Task WriteAsync(HttpContext context, int statusCode, string code, string message)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(ApiResponses.Payload(code, message), context.RequestAborted);
    }
}
