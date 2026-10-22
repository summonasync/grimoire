using System.ComponentModel;
using System.Diagnostics;
using Grimoire.Core;

namespace Grimoire.Infrastructure.Processes;

public sealed class SystemProcess : IExternalProcess
{
    public async Task<int> RunAsync(string file, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(file) { UseShellExecute = false };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Spawn(startInfo, file);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            KillTreeQuietly(process);
            throw;
        }

        return process.ExitCode;
    }

    private static void KillTreeQuietly(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception racing) when (racing is InvalidOperationException or Win32Exception)
        {
        }
    }

    private static Process Spawn(ProcessStartInfo startInfo, string file)
    {
        try
        {
            return Process.Start(startInfo)
                ?? throw new ProcessSpawnException(file, "the system created no process.");
        }
        catch (Exception failure) when (failure is Win32Exception or InvalidOperationException)
        {
            throw new ProcessSpawnException(file, failure.Message, failure);
        }
    }
}
