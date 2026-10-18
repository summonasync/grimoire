using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record MemoryListResponse(string Tome, int Count, IReadOnlyList<MemoryRecord> Items);
