using Grimoire.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Grimoire.Api;

public sealed class GcSweeper(
    Func<CancellationToken, Task<GcResult>> gc,
    IClock clock,
    TimeSpan interval,
    IGcStateStore? gcState = null,
    Func<TimeSpan, CancellationToken, Task>? pause = null,
    ILogger<GcSweeper>? logger = null) : BackgroundService
{
    private readonly Func<CancellationToken, Task<GcResult>> _gc = gc ?? throw new ArgumentNullException(nameof(gc));
    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly TimeSpan _interval = ValidateInterval(interval);
    private readonly IGcStateStore? _gcState = gcState;
    private readonly Func<TimeSpan, CancellationToken, Task> _pause =
        pause ?? ((delay, cancellationToken) => Task.Delay(delay, cancellationToken));
    private readonly ILogger<GcSweeper> _logger = logger ?? NullLogger<GcSweeper>.Instance;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _pause(RemainingDelay(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var attemptStartedAt = _clock.UtcNow;
            try
            {
                var result = await _gc(stoppingToken);
                if (result.TotalRemoved > 0)
                {
                    _logger.LogInformation("GC sweep removed {Removed} expired sessions and {Tombstones} aged tombstones",
                        result.ExpiredSessions, result.AgedTombstones);
                }

                await RecordSuccessAsync(attemptStartedAt, result, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GC sweep failed; retrying after the next interval");
                await RecordFailureAsync(attemptStartedAt, ex, stoppingToken);
            }
        }
    }

    private async Task RecordSuccessAsync(DateTimeOffset attemptStartedAt, GcResult result, CancellationToken stoppingToken)
    {
        if (_gcState is null)
        {
            return;
        }

        try
        {
            await _gcState.WriteAsync(new GcState
            {
                LastAttemptAt = attemptStartedAt,
                LastSuccessAt = _clock.UtcNow,
                RemovedExpired = result.ExpiredSessions,
                RemovedTombstones = result.AgedTombstones,
            }, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure)
        {
            _logger.LogWarning(failure, "GC heartbeat write failed; the sweep itself succeeded");
        }
    }

    private async Task RecordFailureAsync(DateTimeOffset attemptStartedAt, Exception sweepFailure, CancellationToken stoppingToken)
    {
        if (_gcState is null)
        {
            return;
        }

        try
        {
            var current = await _gcState.ReadAsync(stoppingToken);
            await _gcState.WriteAsync(current with
            {
                LastAttemptAt = attemptStartedAt,
                LastError = sweepFailure.Message,
            }, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure)
        {
            _logger.LogWarning(failure, "GC heartbeat write failed on top of a failed sweep");
        }
    }

    private TimeSpan RemainingDelay()
    {
        var due = _clock.UtcNow.Add(_interval);
        var remaining = due - _clock.UtcNow;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private static TimeSpan ValidateInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "The GC sweep interval must be positive.");
        }

        return interval;
    }
}
