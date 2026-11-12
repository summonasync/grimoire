using Grimoire.Api.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace Grimoire.Api;

internal static class SpaHosting
{
    public static SpaStampVerdict UseSpaFrontend(this WebApplication app, ApiHostOptions options)
    {
        var root = Path.GetFullPath(options.StaticRootPath);
        var stamp = SpaStampVerifier.Read(root);
        ReportStamp(app, stamp, root);

        var exportExists = Directory.Exists(root);
        if (exportExists)
        {
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(root) });
        }

        app.MapFallback(context => ServeFallbackAsync(context, root, exportExists));

        return stamp;
    }

    private static void ReportStamp(WebApplication app, SpaStampVerdict stamp, string root)
    {
        if (stamp.WarningLine is not { } warning)
        {
            return;
        }

        app.Services.GetService<ILoggerFactory>()?
            .CreateLogger(nameof(SpaHosting))
            .LogWarning("Deployed SPA release stamp {State} export={ExportVersion} binary={BinaryVersion} cause={Cause} root={StaticRoot}: {Warning}",
                stamp.State, stamp.ExportVersion, stamp.BinaryVersion, stamp.Cause, root, warning);
    }

    private static async Task ServeFallbackAsync(HttpContext context, string root, bool exportExists)
    {
        var response = context.Response;
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            await RespondWithApiStatusAsync(context, response);
            return;
        }

        if (Path.HasExtension(context.Request.Path.Value))
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var index = Path.Combine(root, "index.html");
        if (exportExists && File.Exists(index))
        {
            await response.SendFileAsync(index, context.RequestAborted);
            return;
        }

        response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        response.ContentType = SpaFallbackPage.ContentType;
        await response.WriteAsync(SpaFallbackPage.Html, context.RequestAborted);
    }

    private static async Task RespondWithApiStatusAsync(HttpContext context, HttpResponse response)
    {
        var (known, methods) = ApiRouteMatcher.Match(context.RequestServices, context.Request.Path);
        if (!known)
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            await response.WriteAsJsonAsync(ApiResponses.Payload("not_found", $"No endpoint for '{context.Request.Path}'."), context.RequestAborted);
            return;
        }

        if (!methods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase))
        {
            response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            response.Headers.Allow = string.Join(", ", methods);
            await response.WriteAsJsonAsync(
                ApiResponses.Payload("method_not_allowed", $"'{context.Request.Method}' is not allowed on '{context.Request.Path}'."),
                context.RequestAborted);
            return;
        }

        response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
        await response.WriteAsJsonAsync(
            ApiResponses.Payload("unsupported_media_type", $"Unsupported media type for '{context.Request.Path}'. This API consumes application/json."),
            context.RequestAborted);
    }
}
