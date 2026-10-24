using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record GraphEdgeResponse(Guid From, Guid To, RecordEdgeClass Class, MemoryStatus TargetStatus);
