using Grimoire.Api.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Grimoire.Api;

internal static class OriginGuard
{
    private static readonly string[] LoopbackHosts = ["127.0.0.1", "localhost", "grimoire.localhost"];

    public static void UseOriginGuard(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            var origin = request.Headers.Origin;
            // No Origin means a non-browser client (CLI/native) and stays allowed; a present
            // Origin on a mutation must be loopback, and ambiguous multi-values fail closed.
            if (IsMutation(request.Method) && origin.Count > 0 && !IsLoopbackOrigin(origin))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    ApiResponses.Payload("forbidden_origin", "Mutations carrying a non-loopback Origin are refused."),
                    context.RequestAborted);
                return;
            }

            await next(context);
        });
    }

    private static bool IsMutation(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    private static bool IsLoopbackOrigin(StringValues origin) =>
        origin.Count == 1
        && origin[0] is { } value
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttp
        && LoopbackHosts.Contains(uri.Host);
}