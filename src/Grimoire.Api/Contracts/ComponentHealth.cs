namespace Grimoire.Api.Contracts;

public sealed record ComponentHealth(bool Reachable, string? Error = null)
{
    public static readonly ComponentHealth Up = new(true);

    public static ComponentHealth Down(string error) => new(false, error);
}
