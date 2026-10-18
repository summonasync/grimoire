using Grimoire.Core;

namespace Grimoire.Api.Querying;

public sealed record ParsedMemoryQuery(IReadOnlyList<MemoryStatus> Statuses, MemoryFilter Filter, int Limit);
