namespace Grimoire.Core;

public sealed class PersonaService(IMemoryStore store, IEmbeddingProvider embeddings, IClock clock)
{
    public async Task<PersonaDocument> GetAsync(CancellationToken cancellationToken)
    {
        var records = await store.ListAsync(PersonaFilter(), cancellationToken);

        var document = new PersonaDocument();
        foreach (var record in records.OrderBy(record => record.CreatedAt))
        {
            var section = ParseSection(record);
            if (section is not null)
            {
                document = document.WithSection(section.Value, record.Content);
            }
        }

        return document;
    }

    public async Task<PersonaDocument> SetAsync(PersonaDocument document, MemoryActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        var now = clock.UtcNow;
        var records = await store.ListAsync(PersonaFilter(), cancellationToken);
        var existing = records
            .Select(record => (Record: record, Section: ParseSection(record)))
            .Where(pair => pair.Section is not null)
            .GroupBy(pair => pair.Section!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(pair => pair.Record.CreatedAt).Last().Record);

        foreach (var section in PersonaSectionExtensions.Sections)
        {
            var content = document.Read(section);
            if (existing.TryGetValue(section, out var current) && current.Content == content)
            {
                continue;
            }

            var vector = await embeddings.EmbedAsync(content, cancellationToken);
            var record = existing.TryGetValue(section, out var stale)
                ? stale with
                {
                    Content = content,
                    Actor = actor,
                    CreatedAt = now,
                    LastAccessedAt = now,
                    AccessCount = 0,
                    Status = MemoryStatus.Active,
                }
                : new MemoryRecord
                {
                    Id = Guid.NewGuid(),
                    Content = content,
                    Scope = MemoryScope.Personal,
                    Kind = MemoryKind.Fact,
                    Actor = actor,
                    Tags = [PersonaSectionExtensions.PersonaTag, section.SectionTag()],
                    CreatedAt = now,
                    LastAccessedAt = now,
                };

            await store.UpsertAsync(record, vector, cancellationToken);
        }

        return document;
    }

    private static MemoryFilter PersonaFilter() => new()
    {
        Scopes = [MemoryScope.Personal],
        Kind = MemoryKind.Fact,
        Status = MemoryStatus.Active,
        Tags = [PersonaSectionExtensions.PersonaTag],
    };

    private static PersonaSection? ParseSection(MemoryRecord record)
    {
        var tag = record.Tags.FirstOrDefault(tag => tag.StartsWith(PersonaSectionExtensions.PersonaTag + ':', StringComparison.Ordinal));
        if (tag is null)
        {
            return null;
        }

        return Enum.TryParse<PersonaSection>(tag[(PersonaSectionExtensions.PersonaTag.Length + 1)..], ignoreCase: true, out var section)
            ? section
            : null;
    }
}
