namespace Grimoire.Core.Consolidation;

public interface IClusterSummarizer
{
    string Summarize(IReadOnlyList<MemoryRecord> sources);
}
