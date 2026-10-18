using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class VectorMathTests
{
    [Fact]
    public void CosineSimilarity_ParallelVectors_ReturnsOne()
    {
        var similarity = VectorMath.CosineSimilarity([1f, 2f, 3f], [2f, 4f, 6f]);

        similarity.ShouldBe(1, 1e-9);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        var similarity = VectorMath.CosineSimilarity([1f, 0f], [0f, 1f]);

        similarity.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void CosineSimilarity_OppositeVectors_ReturnsMinusOne()
    {
        var similarity = VectorMath.CosineSimilarity([1f, 0f], [-1f, 0f]);

        similarity.ShouldBe(-1, 1e-9);
    }

    [Fact]
    public void CosineSimilarity_ZeroVector_ReturnsZero()
    {
        var similarity = VectorMath.CosineSimilarity([0f, 0f], [1f, 0f]);

        similarity.ShouldBe(0);
    }

    [Fact]
    public void CosineSimilarity_EmptyVectors_ReturnsZero()
    {
        var similarity = VectorMath.CosineSimilarity([], []);

        similarity.ShouldBe(0);
    }

    [Fact]
    public void CosineSimilarity_LengthMismatch_Throws()
    {
        Should.Throw<ArgumentException>(() => VectorMath.CosineSimilarity([1f, 0f], [1f, 0f, 0f]));
    }
}
