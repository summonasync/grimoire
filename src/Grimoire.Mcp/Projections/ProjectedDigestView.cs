namespace Grimoire.Mcp.Projections;

internal sealed record ProjectedDigestView
{
    public required string Tome { get; init; }

    public required string Topic { get; init; }

    public required DigestProjectedSection LongMemo { get; init; }

    public required DigestProjectedSection Recent { get; init; }

    public required DigestProjectedSection Gone { get; init; }

    public required IReadOnlyList<string> Omitted { get; init; }

    public IReadOnlyList<string>? Warnings { get; init; }
}
