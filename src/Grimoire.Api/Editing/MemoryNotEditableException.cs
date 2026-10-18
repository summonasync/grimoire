using Grimoire.Core;

namespace Grimoire.Api.Editing;

public sealed class MemoryNotEditableException(Guid id, MemoryStatus status)
    : Exception($"Memory '{id}' cannot be edited while its status is '{status}'.");
