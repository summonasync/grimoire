namespace Grimoire.Core;

public readonly record struct MemoryActor(string Alias, string? Vendor)
{
    public const string HumanAlias = "human";

    public static MemoryActor Human { get; } = new(HumanAlias, null);

    public bool IsHuman => Alias.Equals(HumanAlias, StringComparison.OrdinalIgnoreCase);

    public static MemoryActor Agent(string alias, string vendor) => new(alias, vendor);

    public static string? AliasPart(string? actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
        {
            return null;
        }

        var separator = actor.IndexOf('+');
        return separator < 0 ? actor : actor[..separator];
    }

    public override string ToString() => Vendor is null ? Alias : $"{Alias}+{Vendor}";
}
