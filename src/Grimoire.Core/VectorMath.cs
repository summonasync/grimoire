namespace Grimoire.Core;

public static class VectorMath
{
    public static double CosineSimilarity(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException($"Vector length mismatch: {left.Length} vs {right.Length}.");
        }

        if (left.Length == 0)
        {
            return 0;
        }

        var span = left;
        var other = right;

        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;

        for (var i = 0; i < span.Length; i++)
        {
            dot += span[i] * (double)other[i];
            leftMagnitude += span[i] * (double)span[i];
            rightMagnitude += other[i] * (double)other[i];
        }

        if (leftMagnitude == 0 || rightMagnitude == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }
}
