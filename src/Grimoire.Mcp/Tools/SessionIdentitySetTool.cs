using System.ComponentModel;
using Grimoire.Mcp.Projections;
using ModelContextProtocol.Protocol;

namespace Grimoire.Mcp.Tools;

internal sealed class SessionIdentitySetTool(
    SessionIdentityHolder identities,
    SessionActorRefinement refinement)
{
    public const string Name = "session_identity_set";

    public const string Title = "Set this session's actor identity";

    public const string Description =
        "Sets the calling session's actor identity: writes made afterwards carry alias+vendor as their actor " +
        "provenance. alias is required (1-32 characters after trim, no whitespace inside); vendor is optional " +
        "(max 32 characters, empty becomes null). Call it once per session when your runtime gives you a stable " +
        "alias; call it again to re-set - the latest pair wins for later writes. The pair lives in memory for this " +
        "MCP connection only: never persisted, it dies with the session. A configured --actor flag still outranks it.";

    public Task<CallToolResult> Set(
        [Description("Your stable agent alias: 1-32 characters after trim, no whitespace inside.")]
        string alias,
        [Description("Optional runtime or vendor name, max 32 characters; empty or omitted becomes null.")]
        string? vendor = null,
        CancellationToken cancellationToken = default)
    {
        var (actor, error) = SessionIdentityParams.Parse(alias, vendor, nameof(alias), nameof(vendor));
        if (error is not null)
        {
            return Task.FromResult(ToolResults.Failure(error.NothingApplied()));
        }

        identities.Set(actor);
        refinement.IdentitySet(actor);

        return Task.FromResult(ToolResults.Success(new SessionIdentityView
        {
            Alias = actor.Alias,
            Vendor = actor.Vendor,
            AppliesTo = "this MCP session only",
        }));
    }
}
