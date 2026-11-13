using Grimoire.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Grimoire.Api;

public sealed class SessionRetentionSweeper(
    Func<DateTimeOffset, CancellationToken, Task<int>> prune,
    IClock clock,
    TimeSpan retention,
    TimeSpan interval,
    Func<TimeSpan, CancellationToken, Task>? pause = null,
    ILogger<SessionRetentionSweeper>? logger = null) : BackgroundService
{
    private readonly Func<DateTimeOffset, CancellationToken, Task<int>> _prune =
        prune ?? throw new ArgumentNullException(nameof(prune));
    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly TimeSpan _retention = ValidateRetention(retention);
    private readonly TimeSpan _interval = ValidateInterval(interval);
    private readonly Func<TimeSpan, CancellationToken, Task> _pause =
        pause ?? ((delay, cancellationToken) => Task.Delay(delay, cancellationToken));
    private readonly ILogger<SessionRetentionSweeper> _logger = logger ?? NullLogger<SessionRetentionSweeper>.Instance;

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

            try
            {
                var removed = await _prune(_clock.UtcNow - _retention, stoppingToken);
                if (removed > 0)
                {
                    _logger.LogInformation(
                        "Session retention sweep retired {Removed} handshake rows older than {Retention}",
                        removed, _retention);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session retention sweep failed; retrying after the next interval");
            }
        }
    }

    private TimeSpan RemainingDelay()
    {
        var due = _clock.UtcNow.Add(_interval);
        var remaining = due - _clock.UtcNow;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private static TimeSpan ValidateRetention(TimeSpan retention)
    {
        if (retention <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retention), retention, "The session retention window must be positive — a zero or negative window retires live handshake rows.");
        }

        return retention;
    }

    private static TimeSpan ValidateInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "The session retention sweep interval must be positive.");
        }

        return interval;
    }
}
