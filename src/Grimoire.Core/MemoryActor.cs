namespace Grimoire.Core;

public readonly record struct MemoryActor(string Alias, string? Vendor)
{
    public const string HumanAlias = "human";

    public static MemoryActor Human { get; } = new(HumanAlias, null);

    public bool IsHuman => Alias.Equals(HumanAlias, StringComparison.OrdinalIgnoreCase);

    public static MemoryActor Agent(string alias, string vendor) => new(alias, vendor);

    public override string ToString() => Vendor is null ? Alias : $"{Alias}+{Vendor}";
}
