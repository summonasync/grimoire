using Grimoire.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Grimoire.Api;

public sealed class ConsolidationSweeper(
    Func<CancellationToken, Task<ConsolidationResult>> sweep,
    IClock clock,
    TimeSpan interval,
    IConsolidationStateStore? consolidationState = null,
    Func<TimeSpan, CancellationToken, Task>? pause = null,
    ILogger<ConsolidationSweeper>? logger = null) : BackgroundService
{
    private readonly Func<CancellationToken, Task<ConsolidationResult>> _sweep = sweep ?? throw new ArgumentNullException(nameof(sweep));
    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly TimeSpan _interval = ValidateInterval(interval);
    private readonly IConsolidationStateStore? _consolidationState = consolidationState;
    private readonly Func<TimeSpan, CancellationToken, Task> _pause =
        pause ?? ((delay, cancellationToken) => Task.Delay(delay, cancellationToken));
    private readonly ILogger<ConsolidationSweeper> _logger = logger ?? NullLogger<ConsolidationSweeper>.Instance;

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
                var result = await _sweep(stoppingToken);
                if (result.Clusters > 0)
                {
                    _logger.LogInformation("Consolidation sweep merged {Clusters} stale journal clusters into cited facts",
                        result.Clusters);
                }

                await RecordSuccessAsync(attemptStartedAt, result, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Consolidation sweep failed; retrying after the next interval");
                await RecordFailureAsync(attemptStartedAt, ex, stoppingToken);
            }
        }
    }

    private async Task RecordSuccessAsync(DateTimeOffset attemptStartedAt, ConsolidationResult result, CancellationToken stoppingToken)
    {
        if (_consolidationState is null)
        {
            return;
        }

        try
        {
            await _consolidationState.WriteAsync(new ConsolidationState
            {
                LastAttemptAt = attemptStartedAt,
                LastSuccessAt = _clock.UtcNow,
                ConsolidatedClusters = result.Clusters,
            }, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure)
        {
            _logger.LogWarning(failure, "Consolidation heartbeat write failed; the sweep itself succeeded");
        }
    }

    private async Task RecordFailureAsync(DateTimeOffset attemptStartedAt, Exception sweepFailure, CancellationToken stoppingToken)
    {
        if (_consolidationState is null)
        {
            return;
        }

        try
        {
            var current = await _consolidationState.ReadAsync(stoppingToken);
            await _consolidationState.WriteAsync(current with
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
            _logger.LogWarning(failure, "Consolidation heartbeat write failed on top of a failed sweep");
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
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "The consolidation sweep interval must be positive.");
        }

        return interval;
    }
}
