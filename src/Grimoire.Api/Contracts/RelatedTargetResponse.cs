using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record RelatedTargetResponse(Guid Id, string Snippet, MemoryStatus Status, string Tome);
