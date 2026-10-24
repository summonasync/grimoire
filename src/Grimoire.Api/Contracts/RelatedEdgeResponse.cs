using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record RelatedEdgeResponse(RecordEdgeClass Class, GraphDirection Direction, RelatedTargetResponse Target);
