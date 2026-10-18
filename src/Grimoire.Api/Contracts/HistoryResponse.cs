using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record HistoryResponse(string Tome, IReadOnlyList<HistoryEntry> Chain);
