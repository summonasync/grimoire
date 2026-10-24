namespace Grimoire.Core.Tests.Measurement;

public sealed class NearDuplicatePairs
{
    public const int Dimension = 64;

    public const int Seed = 20261024;

    private const double TwinSimilarity = 0.99;

    private static readonly double TwinNoise = Math.Sqrt(1 / (TwinSimilarity * TwinSimilarity) - 1);

    private readonly Random _random = new(Seed);

    private readonly IReadOnlyList<string> _topics = ["tombstone", "compaction", "embedding", "recall", "journal"];

    private int _ordinal;

    private double _spare;

    private bool _hasSpare;

    public IReadOnlyList<string> Topics => _topics;

    public (MemoryRecord Record, float[] Vector) NextJournal(string tome, DateTimeOffset accessed)
    {
        var ordinal = _ordinal++;
        return (Record(tome, accessed, ordinal), UnitVector());
    }

    public IReadOnlyList<(MemoryRecord Record, float[] Vector)> NextTwinJournals(string tome, DateTimeOffset accessed)
    {
        var first = UnitVector();
        var second = TwinOf(first);

        return
        [
            (Record(tome, accessed, _ordinal++), first),
            (Record(tome, accessed, _ordinal++), second),
        ];
    }

    public float[] UnitVector()
    {
        var vector = new float[Dimension];
        for (var i = 0; i < Dimension; i++)
        {
            vector[i] = (float)NextGaussian();
        }

        return Normalize(vector);
    }

    private float[] TwinOf(float[] vector)
    {
        var direction = UnitVector();
        var noisy = new float[Dimension];
        for (var i = 0; i < Dimension; i++)
        {
            noisy[i] = vector[i] + (float)(TwinNoise * direction[i]);
        }

        return Normalize(noisy);
    }

    private MemoryRecord Record(string tome, DateTimeOffset accessed, int ordinal)
    {
        var topic = _topics[ordinal % _topics.Count];
        var content =
            $"{char.ToUpperInvariant(topic[0])}{topic[1..]} {_topics[(ordinal + 1) % _topics.Count]} {_topics[(ordinal + 2) % _topics.Count]} stale journal entry {ordinal}";

        return TestMemories.Create(
            content,
            [],
            accessed: accessed,
            tome: tome,
            tags: [topic, "journal", tome]);
    }

    private double NextGaussian()
    {
        if (_hasSpare)
        {
            _hasSpare = false;
            return _spare;
        }

        var magnitude = Math.Sqrt(-2.0 * Math.Log(1.0 - _random.NextDouble()));
        var angle = 2.0 * Math.PI * _random.NextDouble();

        _spare = magnitude * Math.Sin(angle);
        _hasSpare = true;

        return magnitude * Math.Cos(angle);
    }

    private static float[] Normalize(float[] vector)
    {
        var sumOfSquares = 0d;
        foreach (var component in vector)
        {
            sumOfSquares += component * component;
        }

        var magnitude = Math.Sqrt(sumOfSquares);
        var normalized = new float[vector.Length];
        for (var i = 0; i < vector.Length; i++)
        {
            normalized[i] = (float)(vector[i] / magnitude);
        }

        return normalized;
    }
}
