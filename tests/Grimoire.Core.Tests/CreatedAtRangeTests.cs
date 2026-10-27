using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class CreatedAtRangeTests
{
    private static readonly TimeZoneInfo PlusTwo = TimeZoneInfo.CreateCustomTimeZone(
        "test-utc-plus-two", TimeSpan.FromHours(2), "UTC+02:00", "UTC+02:00");

    [Fact]
    public void Parse_DateOnlyBounds_ResolveToLocalMidnightAsUtc()
    {
        var range = CreatedAtRange.Parse("2026-10-01", "2026-10-07", PlusTwo);

        range.Since.ShouldBe(new DateTimeOffset(2026, 9, 30, 22, 0, 0, TimeSpan.Zero));
        range.Until.ShouldBe(new DateTimeOffset(2026, 10, 6, 22, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Parse_FullTimestamps_KeepTheirInstant()
    {
        var range = CreatedAtRange.Parse("2026-10-01T08:30:00Z", "2026-10-07T23:59:59+02:00", PlusTwo);

        range.Since.ShouldBe(new DateTimeOffset(2026, 10, 1, 8, 30, 0, TimeSpan.Zero));
        range.Until.ShouldBe(new DateTimeOffset(2026, 10, 7, 21, 59, 59, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_BlankBounds_OpenRange(string? bound)
    {
        var range = CreatedAtRange.Parse(bound, bound, PlusTwo);

        range.Since.ShouldBeNull();
        range.Until.ShouldBeNull();
    }

    [Theory]
    [InlineData("not-a-date", "2026-10-01", "since")]
    [InlineData("2026-10-01", "not-a-date", "until")]
    [InlineData("2026-10-01", "2026-13-40", "until")]
    public void Parse_UnparsableBound_ThrowsNamingTheParameter(string? since, string? until, string expectedParam)
    {
        var failure = Should.Throw<ArgumentException>(() => CreatedAtRange.Parse(since, until, PlusTwo));

        failure.ParamName.ShouldBe(expectedParam);
        failure.Message.ShouldContain("ISO-8601");
    }

    [Fact]
    public void Parse_SinceAfterUntil_ThrowsOnSince()
    {
        var failure = Should.Throw<ArgumentException>(
            () => CreatedAtRange.Parse("2026-10-07", "2026-10-01", PlusTwo));

        failure.ParamName.ShouldBe("since");
    }

    [Fact]
    public void Parse_SinceEqualsUntil_EmptyWindowIsLegal()
    {
        var range = CreatedAtRange.Parse("2026-10-01T12:00:00Z", "2026-10-01T12:00:00Z", PlusTwo);

        range.Since.ShouldBe(range.Until);
    }

    [Fact]
    public void Parse_MixedDateOnlyAndTimestamp_Composes()
    {
        var range = CreatedAtRange.Parse("2026-10-01", "2026-10-02T05:00:00Z", PlusTwo);

        range.Since.ShouldBe(new DateTimeOffset(2026, 9, 30, 22, 0, 0, TimeSpan.Zero));
        range.Until.ShouldBe(new DateTimeOffset(2026, 10, 2, 5, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Parse_WithoutZone_ResolvesDateOnlyAgainstServerZone()
    {
        var date = new DateTime(2026, 10, 1);
        var expected = new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date)).ToUniversalTime();

        var range = CreatedAtRange.Parse("2026-10-01", null);

        range.Since.ShouldBe(expected);
    }
}
