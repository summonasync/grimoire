using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record RestoreMemoryResponse(
    MemoryRecord Record,
    string Tome,
    bool Restored,
    SupersederState? Superseder);
