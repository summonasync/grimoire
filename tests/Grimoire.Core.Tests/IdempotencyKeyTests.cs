using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class IdempotencyKeyTests
{
    [Fact]
    public void Compute_IsDeterministicForIdenticalRequests()
    {
        IdempotencyKey.Compute(Request()).ShouldBe(IdempotencyKey.Compute(Request()));
    }

    [Fact]
    public void Compute_IsSixtyFourLowercaseHexCharacters()
    {
        var key = IdempotencyKey.Compute(Request());

        key.Length.ShouldBe(64);
        key.ShouldAllBe(character => char.IsAsciiHexDigit(character) && !char.IsAsciiLetterUpper(character));
    }

    [Fact]
    public void Compute_ChangesWithContent()
    {
        IdempotencyKey.Compute(Request(content: "deploy on friday"))
            .ShouldNotBe(IdempotencyKey.Compute(Request(content: "deploy on fridays")));
    }

    [Fact]
    public void Compute_ChangesWithScope()
    {
        IdempotencyKey.Compute(Request(scope: MemoryScope.Project))
            .ShouldNotBe(IdempotencyKey.Compute(Request(scope: MemoryScope.Global)));
    }

    [Fact]
    public void Compute_ChangesWithKind()
    {
        IdempotencyKey.Compute(Request(kind: MemoryKind.Journal))
            .ShouldNotBe(IdempotencyKey.Compute(Request(kind: MemoryKind.Fact)));
    }

    [Fact]
    public void Compute_ChangesWithActorIdentity()
    {
        var otherVendor = Request() with { Actor = MemoryActor.Agent("dotnet-coder", "gpt") };
        var otherAlias = Request() with { Actor = MemoryActor.Agent("web-coder", "claude") };

        IdempotencyKey.Compute(Request()).ShouldNotBe(IdempotencyKey.Compute(otherVendor));
        IdempotencyKey.Compute(Request()).ShouldNotBe(IdempotencyKey.Compute(otherAlias));
    }

    [Fact]
    public void Compute_ChangesWithProject()
    {
        IdempotencyKey.Compute(Request(projectId: "grim"))
            .ShouldNotBe(IdempotencyKey.Compute(Request(projectId: "other")));
    }

    [Fact]
    public void Compute_ChangesWithSession()
    {
        IdempotencyKey.Compute(Request(sessionId: "s-1"))
            .ShouldNotBe(IdempotencyKey.Compute(Request(sessionId: "s-2")));
    }

    [Fact]
    public void Compute_NormalizesAbsentOptionalPartsToEmpty()
    {
        var absentProject = Request(projectId: null);
        var emptyProject = Request(projectId: string.Empty);
        var absentSession = Request(sessionId: null);
        var emptySession = Request(sessionId: string.Empty);
        var absentVendor = Request(vendor: null);
        var emptyVendor = Request(vendor: string.Empty);

        IdempotencyKey.Compute(absentProject).ShouldBe(IdempotencyKey.Compute(emptyProject));
        IdempotencyKey.Compute(absentSession).ShouldBe(IdempotencyKey.Compute(emptySession));
        IdempotencyKey.Compute(absentVendor).ShouldBe(IdempotencyKey.Compute(emptyVendor));
    }

    [Fact]
    public void Compute_LengthPrefixSeparatesBoundaryShift()
    {
        var shifted = Request(projectId: "xa", content: "b");
        var original = Request(projectId: "x", content: "ab");

        IdempotencyKey.Compute(shifted).ShouldNotBe(IdempotencyKey.Compute(original));
    }

    [Fact]
    public void Compute_LengthPrefixSeparatesSeparatorForging()
    {
        var forged = Request(projectId: "x\x1fa", content: "b");
        var honest = Request(projectId: "x", content: "a\x1fb");

        IdempotencyKey.Compute(forged).ShouldNotBe(IdempotencyKey.Compute(honest));
    }

    private static RememberRequest Request(
        string content = "deploy on friday",
        MemoryScope scope = MemoryScope.Project,
        MemoryKind kind = MemoryKind.Journal,
        string? projectId = "grim",
        string? sessionId = null,
        string? vendor = "claude") => new()
        {
            Content = content,
            Scope = scope,
            Kind = kind,
            Actor = new MemoryActor("dotnet-coder", vendor),
            ProjectId = projectId,
            SessionId = sessionId,
        };
}
