using System.Diagnostics;
using Grimoire.Core;
using Grimoire.Infrastructure.Processes;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Processes;

public sealed class SystemProcessTests
{
    [Fact]
    public async Task RunAsync_NonexistentExecutable_ThrowsProcessSpawnException()
    {
        var process = new SystemProcess();

        var failure = await Should.ThrowAsync<ProcessSpawnException>(
            () => process.RunAsync("/definitely/not/an/executable", [], CancellationToken.None));

        failure.File.ShouldBe("/definitely/not/an/executable");
    }

    [Fact]
    public async Task RunAsync_ChildExitsNonZero_ReturnsExitCode()
    {
        var process = new SystemProcess();

        var exitCode = await process.RunAsync("/bin/sh", ["-c", "exit 7"], CancellationToken.None);

        exitCode.ShouldBe(7);
    }

    [Fact]
    public async Task RunAsync_CancellationDuringRun_KillsChildAndPropagates()
    {
        var pidFile = Path.Combine(Path.GetTempPath(), $"grimoire-child-{Guid.NewGuid():N}.pid");
        using var cts = new CancellationTokenSource();
        var process = new SystemProcess();
        var running = process.RunAsync("/bin/sh", ["-c", $"echo $$ > {pidFile}; sleep 30"], cts.Token);
        try
        {
            var childPid = await ReadChildPidAsync(pidFile);
            await cts.CancelAsync();

            await Should.ThrowAsync<OperationCanceledException>(() => running);
            await WaitUntilGoneAsync(childPid);
        }
        finally
        {
            await cts.CancelAsync();
            try
            {
                await running;
            }
            catch (OperationCanceledException)
            {
            }

            if (File.Exists(pidFile))
            {
                File.Delete(pidFile);
            }
        }
    }

    private static async Task<int> ReadChildPidAsync(string pidFile)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (true)
        {
            if (File.Exists(pidFile))
            {
                var text = await File.ReadAllTextAsync(pidFile);
                if (text.EndsWith('\n') && int.TryParse(text.Trim(), out var pid) && pid > 0)
                {
                    return pid;
                }
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"child pid was not written to {pidFile}");
            }

            await Task.Delay(25);
        }
    }

    private static async Task WaitUntilGoneAsync(int childPid)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (IsAlive(childPid))
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"child process {childPid} survived cancellation");
            }

            await Task.Delay(25);
        }
    }

    private static bool IsAlive(int childPid)
    {
        try
        {
            using var candidate = Process.GetProcessById(childPid);
            return !candidate.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
