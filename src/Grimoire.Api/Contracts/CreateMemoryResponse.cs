using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record CreateMemoryResponse(MemoryRecord Record, Guid? SupersededId);
