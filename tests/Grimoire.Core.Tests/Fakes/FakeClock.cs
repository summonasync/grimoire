namespace Grimoire.Core.Tests.Fakes;

public sealed class FakeClock(DateTimeOffset? start = null) : IClock
{
    public static readonly DateTimeOffset DefaultStart = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _utcNow = start ?? DefaultStart;

    public DateTimeOffset UtcNow => _utcNow;

    public void Advance(TimeSpan time) => _utcNow += time;

    public void MoveTo(DateTimeOffset moment) => _utcNow = moment;
}
