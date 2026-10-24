namespace Grimoire.Core;

public sealed class AutoMemoryImporter(
    MemoryService memories,
    IMemoryStore store,
    IEmbeddingProvider embeddings,
    IClock clock,
    MemoryEngineOptions? options = null)
{
    private const int DuplicateProbeLimit = 5;

    private readonly MemoryEngineOptions _options = options ?? new();

    public async Task<AutoMemoryImportSummary> ImportAsync(
        string sourceDirectory,
        string? tome,
        MemoryActor actor,
        string? projectId,
        CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var stateRead = await AutoMemoryImportState.ReadAsync(sourceDirectory, cancellationToken);
        if (stateRead.Warning is { } warning)
        {
            warnings.Add(warning);
        }

        var resolvedTome = TomeKey.Normalize(tome ?? _options.DefaultTome);
        var parsed = await AutoMemoryFileParser.ParseAsync(sourceDirectory, cancellationToken);
        Dictionary<string, string> hashes = stateRead.State?.Hashes is { } known
            ? new Dictionary<string, string>(known)
            : [];
        var outcomes = new List<AutoMemoryFileOutcome>(parsed.Failures.Count);

        foreach (var failure in parsed.Failures)
        {
            outcomes.Add(new AutoMemoryFileOutcome
            {
                FileName = failure.FileName,
                Kind = AutoMemoryOutcomeKind.Failed,
                Reason = failure.Reason,
            });
        }

        foreach (var candidate in parsed.Candidates)
        {
            var hash = AutoMemoryImportState.ContentHash(candidate.Content);
            if (stateRead.State is { } previous && !Selected(candidate, hash, previous))
            {
                continue;
            }

            var outcome = await ImportCandidateAsync(candidate, resolvedTome, actor, projectId, cancellationToken);
            outcomes.Add(outcome);

            if (outcome.Kind is not AutoMemoryOutcomeKind.Failed)
            {
                hashes[candidate.FileName] = hash;
            }
        }

        var marker = new AutoMemoryImportState
        {
            ImportedAt = clock.UtcNow,
            SourcePath = sourceDirectory,
            Hashes = hashes,
        };

        try
        {
            await AutoMemoryImportState.WriteAsync(sourceDirectory, marker, cancellationToken);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"import marker could not be written: {failure.Message}");
        }

        return new AutoMemoryImportSummary
        {
            Files = parsed.Candidates.Count + parsed.Failures.Count,
            Unchanged = parsed.Candidates.Count + parsed.Failures.Count - outcomes.Count,
            Warnings = warnings,
            Outcomes = outcomes,
        };
    }

    private static bool Selected(AutoMemoryCandidate candidate, string hash, AutoMemoryImportState previous) =>
        !previous.Hashes.TryGetValue(candidate.FileName, out var known)
        || candidate.ModifiedAt > previous.ImportedAt
        || !string.Equals(known, hash, StringComparison.Ordinal);

    private async Task<AutoMemoryFileOutcome> ImportCandidateAsync(
        AutoMemoryCandidate candidate,
        string tome,
        MemoryActor actor,
        string? projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            var winner = await FindDuplicateAsync(candidate.Content, tome, projectId, cancellationToken);
            if (winner is not null)
            {
                return new AutoMemoryFileOutcome
                {
                    FileName = candidate.FileName,
                    Kind = AutoMemoryOutcomeKind.SkippedDuplicate,
                    WinnerId = winner,
                };
            }

            var remembered = await memories.RememberAsync(new RememberRequest
            {
                Content = candidate.Content,
                Scope = MemoryScope.Project,
                Kind = MemoryKind.Fact,
                Actor = actor,
                Tome = tome,
                ProjectId = projectId,
                Tags = [$"auto-memory:{candidate.FileName}"],
            }, cancellationToken);

            return new AutoMemoryFileOutcome
            {
                FileName = candidate.FileName,
                Kind = AutoMemoryOutcomeKind.Imported,
                RecordId = remembered.Record.Id,
            };
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return new AutoMemoryFileOutcome
            {
                FileName = candidate.FileName,
                Kind = AutoMemoryOutcomeKind.Failed,
                Reason = failure.Message,
            };
        }
    }

    private async Task<Guid?> FindDuplicateAsync(string content, string tome, string? projectId, CancellationToken cancellationToken)
    {
        var vector = await embeddings.EmbedAsync(content, cancellationToken);
        var matches = await store.SearchAsync(vector, ProbeFilter(tome, projectId), DuplicateProbeLimit, cancellationToken);

        return matches
            .Where(match => match.Similarity >= _options.DedupSimilarityThreshold)
            .OrderByDescending(match => match.Similarity)
            .ThenBy(match => match.Record.Id)
            .Select(match => (Guid?)match.Record.Id)
            .FirstOrDefault();
    }

    private MemoryFilter ProbeFilter(string tome, string? projectId) => new()
    {
        Scopes = [MemoryScope.Project],
        Kind = MemoryKind.Fact,
        Status = MemoryStatus.Active,
        Tome = tome,
        ProjectId = projectId,
        ExcludeTags = [PersonaSectionExtensions.PersonaTag],
    };
}
