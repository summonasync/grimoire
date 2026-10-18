using Serilog;
using Serilog.Events;

namespace Grimoire.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // stdout carries the mcp face protocol, so every log line must reach stderr.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
            .CreateLogger();

        try
        {
            return await CliApplication.RunAsync(args);
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
