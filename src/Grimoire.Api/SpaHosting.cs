using Grimoire.Api.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace Grimoire.Api;

internal static class SpaHosting
{
    public static void UseSpaFrontend(this WebApplication app, ApiHostOptions options)
    {
        var root = Path.GetFullPath(options.StaticRootPath);
        var exportExists = Directory.Exists(root);
        if (exportExists)
        {
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(root) });
        }

        app.MapFallback(context => ServeFallbackAsync(context, root, exportExists));
    }

    private static async Task ServeFallbackAsync(HttpContext context, string root, bool exportExists)
    {
        var response = context.Response;
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            await response.WriteAsJsonAsync(ApiResponses.Payload("not_found", $"No endpoint for '{context.Request.Path}'."), context.RequestAborted);
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
}
