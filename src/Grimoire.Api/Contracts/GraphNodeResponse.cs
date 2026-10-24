using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record GraphNodeResponse(Guid Id, string Snippet, MemoryKind Kind, string? Project);
