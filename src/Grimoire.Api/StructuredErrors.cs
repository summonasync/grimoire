using Grimoire.Api.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api;

internal static class StructuredErrors
{
    public static void UseStructuredErrors(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            await next(context);
            if (context.Response.StatusCode == StatusCodes.Status400BadRequest && !context.Response.HasStarted)
            {
                await context.Response.WriteAsJsonAsync(
                    ApiResponses.Payload(
                        "invalid_request",
                        "The request body is missing, malformed, or does not match the expected schema."),
                    context.RequestAborted);
            }
        });
    }
}
