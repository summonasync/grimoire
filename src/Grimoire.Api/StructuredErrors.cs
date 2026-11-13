using Grimoire.Api.Endpoints;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api;

internal static class StructuredErrors
{
    private const string BodyFault =
        "The request body is missing, malformed, or does not match the expected schema.";

    private const string ParameterFault =
        "A route or query parameter of this request is missing or malformed.";

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
                        AcceptsBody(context.Request.Method) ? BodyFault : ParameterFault);
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
