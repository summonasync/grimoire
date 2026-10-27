using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class ReadProjectionTests
{
    private static readonly ReadProjectionSurface Surface = new()
    {
        AllowedKeys = ["memoryId", "content", "tags", "createdAt", "score"],
        AlwaysOnKeys = ["memoryId"],
        CompactKeys = ["memoryId", "content", "tags", "createdAt"],
    };

    [Fact]
    public void Resolve_FieldsNull_ReturnsNull()
    {
        ReadProjection.Resolve(null, compact: false, Surface).ShouldBeNull();
    }

    [Fact]
    public void Resolve_FieldsEmpty_BehavesAsOmitted()
    {
        ReadProjection.Resolve([], compact: false, Surface).ShouldBeNull();
    }

    [Fact]
    public void Resolve_FieldsEmptyWithCompact_ResolvesCompactSet()
    {
        ReadProjection.Resolve([], compact: true, Surface)
            .ShouldBe(["memoryId", "content", "tags", "createdAt"]);
    }

    [Fact]
    public void Resolve_FieldsWithCompact_ThrowsConflict()
    {
        var error = Should.Throw<ReadProjectionValidationException>(() =>
            ReadProjection.Resolve(["content"], compact: true, Surface));

        error.Message.ShouldContain("fields");
        error.Message.ShouldContain("compact");
    }

    [Fact]
    public void Resolve_UnknownField_ThrowsNamingAllowedSet()
    {
        var error = Should.Throw<ReadProjectionValidationException>(() =>
            ReadProjection.Resolve(["content", "body"], compact: false, Surface));

        error.Message.ShouldContain("body");
        foreach (var key in Surface.AllowedKeys)
        {
            error.Message.ShouldContain(key);
        }
    }

    [Fact]
    public void Resolve_Compact_ResolvesSurfaceCompactSet()
    {
        ReadProjection.Resolve(null, compact: true, Surface)
            .ShouldBe(Surface.CompactKeys);
    }

    [Fact]
    public void Resolve_Fields_MergesAlwaysOnAheadOfRequest()
    {
        ReadProjection.Resolve(["tags", "score"], compact: false, Surface)
            .ShouldBe(["memoryId", "tags", "score"]);
    }

    [Fact]
    public void Resolve_ExplicitAlwaysOnField_IsAcceptedWithoutDuplication()
    {
        ReadProjection.Resolve(["memoryId", "content"], compact: false, Surface)
            .ShouldBe(["memoryId", "content"]);
    }

    [Fact]
    public void Resolve_DuplicateRequestedFields_AppearOnce()
    {
        ReadProjection.Resolve(["tags", "tags"], compact: false, Surface)
            .ShouldBe(["memoryId", "tags"]);
    }

    [Theory]
    [InlineData("MemoryId")]
    [InlineData(" memoryId")]
    public void Resolve_FieldNotExactlyMatchingAllowed_Throws(string field)
    {
        Should.Throw<ReadProjectionValidationException>(() =>
            ReadProjection.Resolve([field], compact: false, Surface));
    }

    [Fact]
    public void Resolve_AlwaysOnFieldOutsideAllowedSet_IsAccepted()
    {
        ReadProjectionSurface sparse = new()
        {
            AllowedKeys = ["content"],
            AlwaysOnKeys = ["memoryId"],
            CompactKeys = ["memoryId", "content"],
        };

        ReadProjection.Resolve(["memoryId"], compact: false, sparse)
            .ShouldBe(["memoryId"]);
    }
}
