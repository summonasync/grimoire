using System.Globalization;
using Grimoire.Api.Sessions;
using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class SessionHandshakeProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProjectAsync_WithoutWindowAskesTheInjectedRetention()
    {
        var registry = new FakeSessionRegistry();
        var clock = new FakeClock(Now);
        var projection = new SessionHandshakeProjection(registry, clock, retentionHours: 6);

        var response = await projection.ProjectAsync(null, null, CancellationToken.None);

        response.WindowHours.ShouldBe(6);
        registry.LastNotBefore.ShouldBe(Now.AddHours(-6));
    }

    [Fact]
    public async Task ProjectAsync_ClampsWindowBounds()
    {
        var registry = new FakeSessionRegistry();
        var projection = new SessionHandshakeProjection(registry, new FakeClock(Now), retentionHours: 6);

        var low = await projection.ProjectAsync(null, -2, CancellationToken.None);
        var high = await projection.ProjectAsync(null, 999_999, CancellationToken.None);

        low.WindowHours.ShouldBe(0);
        high.WindowHours.ShouldBe(8760);
    }

    [Fact]
    public async Task ProjectAsync_ClampsLimitBounds()
    {
        var registry = new FakeSessionRegistry();
        var projection = new SessionHandshakeProjection(registry, new FakeClock(Now), retentionHours: 6);

        var low = await projection.ProjectAsync(0, null, CancellationToken.None);
        var high = await projection.ProjectAsync(9999, null, CancellationToken.None);

        low.Limit.ShouldBe(1);
        high.Limit.ShouldBe(500);
    }

    [Theory]
    [InlineData(-90.4, 1.51)]
    [InlineData(-89.6, 1.49)]
    [InlineData(-7.5, 0.12)]
    [InlineData(-45, 0.75)]
    public async Task ProjectAsync_RoundsAgeToTwoDecimals(double minutes, double expectedAgeHours)
    {
        var registry = new FakeSessionRegistry();
        registry.Seed(Entry(Now.AddMinutes(minutes)));

        var response = await new SessionHandshakeProjection(registry, new FakeClock(Now), 6)
            .ProjectAsync(null, null, CancellationToken.None);

        response.Items.ShouldHaveSingleItem();
        response.Items[0].AgeHours.ShouldBe(expectedAgeHours);
        response.Items[0].Host.ShouldBe("workstation");
        response.Items[0].ProtocolVersion.ShouldBeNull();
    }

    [Fact]
    public async Task ProjectAsync_CarriesTheRegistryDegradationReason()
    {
        var registry = new FakeSessionRegistry { DegradationReason = "unreachable" };

        var response = await new SessionHandshakeProjection(registry, new FakeClock(Now), 6)
            .ProjectAsync(null, null, CancellationToken.None);

        response.DegradationReason.ShouldBe("unreachable");
        response.Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public async Task ProjectAsync_NonFiniteWindowAsksTheInjectedRetention(string candidate)
    {
        var registry = new FakeSessionRegistry();
        var projection = new SessionHandshakeProjection(registry, new FakeClock(Now), retentionHours: 6);

        var response = await projection.ProjectAsync(
            null,
            double.Parse(candidate, CultureInfo.InvariantCulture),
            CancellationToken.None);

        response.WindowHours.ShouldBe(6);
        registry.LastNotBefore.ShouldBe(Now.AddHours(-6));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void Constructor_NonFiniteRetentionDefault_RefusesTheProjectionBeforeAnyRequest(string candidate)
    {
        // The argument net substitutes this number when a caller asks no window, so a
        // non-finite default would leave the guard, survive the clamp and reach the response
        // as a number no JSON writer can emit — the refusal belongs to the composition, not
        // to a fourth check inside the request path.
        var hours = double.Parse(candidate, CultureInfo.InvariantCulture);

        var failure = Should.Throw<ArgumentOutOfRangeException>(
            () => new SessionHandshakeProjection(new FakeSessionRegistry(), new FakeClock(Now), hours));

        failure.ParamName.ShouldBe("retentionHours");
    }

    private static SessionRegistryEntry Entry(DateTimeOffset occurredAt) => new()
    {
        OccurredAt = occurredAt,
        ClientName = "qwen",
        ClientVersion = "1.0.0",
        ProtocolVersion = null,
        ActorAlias = "guldana",
        ActorVendor = "qwen",
        Transport = "stdio",
        Host = "workstation",
    };
}
