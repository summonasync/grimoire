namespace Grimoire.Core;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
