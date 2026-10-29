using Grimoire.Api.Contracts;
using Grimoire.Core;

namespace Grimoire.Api.Editing;

public sealed class MemoryEditor(IMemoryStore store, IEmbeddingProvider embeddings, IClock clock)
{
    public async Task<MemoryRecord?> EditAsync(
        Guid id,
        PatchMemoryRequest patch,
        MemoryActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(patch);

        if (string.IsNullOrWhiteSpace(patch.Reason))
        {
            throw new ArgumentException("A patch reason is required.", nameof(patch));
        }

        if (patch.Content is not null && string.IsNullOrWhiteSpace(patch.Content))
        {
            throw new ArgumentException("Memory content must not be empty.", nameof(patch));
        }

        if (patch.IsEmpty)
        {
            throw new ArgumentException("The patch carries no fields to apply.", nameof(patch));
        }

        var record = await store.GetAsync(id, cancellationToken);
        if (record is null)
        {
            return null;
        }

        if (record.Status != MemoryStatus.Active)
        {
            throw new MemoryNotEditableException(id, record.Status);
        }

        var contentChanged = patch.Content is not null && patch.Content != record.Content;
        var updated = record with
        {
            Content = contentChanged ? patch.Content! : record.Content,
            Tags = patch.Tags ?? record.Tags,
            Importance = patch.Importance is null ? record.Importance : Math.Clamp(patch.Importance.Value, 0, 1),
        };
        var corrected = CorrectionTrail.Append(updated, clock.UtcNow, actor, CorrectionAction.Patch, patch.Reason);

        if (contentChanged)
        {
            var vector = await embeddings.EmbedAsync(updated.Content, cancellationToken);
            await store.UpsertAsync(corrected, vector, cancellationToken);
        }
        else
        {
            await store.UpdateAsync(corrected, cancellationToken);
        }

        return corrected;
    }
}
