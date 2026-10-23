using Grimoire.Core;

namespace Grimoire.Api.Endpoints;

internal static class ProjectTagErrors
{
    public static string Code(ProjectTagRejection rejection) => rejection switch
    {
        ProjectTagRejection.Unknown => "project_id_unknown",
        _ => "project_id_required",
    };

    public static string Message(
        ProjectTagRejection rejection,
        ProjectTagResolution resolution,
        string? projectId,
        string callerPath)
    {
        var allowed = string.Join(", ", resolution.AllowedAliases);
        return rejection switch
        {
            ProjectTagRejection.Unknown => resolution.AllowedAliases.Count == 0
                ? $"projectId '{projectId}' is not a registered project and the project registry is empty - " +
                  "register the project in the Grimoire config first."
                : $"projectId: '{projectId}' is not a registered project alias. Allowed projectId values " +
                  $"(case-sensitive, pass one exactly as advertised): {allowed}.",
            _ => "scope 'project' requires projectId (got scope 'project', no projectId): the API server's working " +
                 $"directory '{callerPath}' matches no registered project path. " +
                 (resolution.AllowedAliases.Count == 0
                     ? "The project registry is empty; register the project in the Grimoire config first. "
                     : $"Pass one of the registered projectId values (case-sensitive, pass one exactly as advertised): {allowed}. ") +
                 "Or use scope 'global' when the memory is not project-specific.",
        };
    }
}
