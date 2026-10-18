namespace Grimoire.Api;

internal static class SpaFallbackPage
{
    public const string ContentType = "text/html; charset=utf-8";

    public const string Html = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>Grimoire</title>
          <style>
            body { font-family: system-ui, sans-serif; max-width: 42rem; margin: 4rem auto; padding: 0 1rem; color: #1c1c1c; }
            code { background: #f2f2f2; padding: 0.1rem 0.3rem; border-radius: 3px; }
          </style>
        </head>
        <body>
          <h1>Grimoire web UI is not deployed</h1>
          <p>The memory API is up and answering on this port, but no static SPA export was found in the configured directory.</p>
          <p>Build the web client (<code>npm run build</code> inside <code>web/</code>), point the serve configuration at its
             output directory and restart <code>grimoire serve</code>. The REST endpoints under <code>/api</code> work meanwhile.</p>
        </body>
        </html>
        """;
}
