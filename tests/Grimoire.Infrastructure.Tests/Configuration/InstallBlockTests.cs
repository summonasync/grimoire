using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Configuration;

public sealed class InstallBlockTests
{
    [Fact]
    public void Text_MatchesTheGateApprovedBlockVerbatim()
    {
        const string approved = """
            <!-- grimoire:begin -->
            ## Grimoire — agent memory

            Grimoire is the local memory service. At session start call `memory_brief` — it loads persona, standing facts, recent journal, tombstones and the TOME TABLE in one read. Record durable facts with `memory_remember` the moment they appear; tombstone wrong ones with `memory_forget`; check `memory_recall` before asserting anything that might be on record.

            **Tome routing**: the brief's `tomes` section is the routing table — write each memory to the tome whose description fits (project work, machine/environment, communication style, memory-about-memory are the usual domains). Create a tome with `memory_tome_create(key, description)` only when no existing tome fits; the description must say what belongs there. Agents never hard-delete tomes — `memory_forget` (tombstone) is the ceiling; hard delete is the human console door.
            <!-- grimoire:end -->
            """;

        InstallBlock.Text.ShouldBe(approved);
    }

    [Fact]
    public void Text_IsFramedByTheMarkerPair()
    {
        InstallBlock.Text.ShouldStartWith(InstallBlock.BeginMarker);
        InstallBlock.Text.ShouldEndWith(InstallBlock.EndMarker);
    }
}
