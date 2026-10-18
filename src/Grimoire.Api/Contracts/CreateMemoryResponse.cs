using Grimoire.Core;

namespace Grimoire.Api.Contracts;

public sealed record CreateMemoryResponse(
    MemoryRecord Record,
    string Tome,
    Guid? SupersededId,
    bool IdempotentReplay = false,
    bool MaintenancePending = false);
