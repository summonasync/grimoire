using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Api;

internal static class ApiRouteMatcher
{
    public static (bool Known, IReadOnlyList<string> Methods) Match(IServiceProvider services, PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var policyFactory = services.GetRequiredService<ParameterPolicyFactory>();
        List<string>? methods = null;
        foreach (var endpoint in services.GetRequiredService<EndpointDataSource>().Endpoints)
        {
            if (endpoint is not RouteEndpoint { RoutePattern: { } pattern } ||
                endpoint.Metadata.GetMetadata<IHttpMethodMetadata>() is not { } httpMethods ||
                !MatchesPattern(pattern, segments, policyFactory))
            {
                continue;
            }

            methods ??= [];
            foreach (var httpMethod in httpMethods.HttpMethods)
            {
                if (!methods.Contains(httpMethod, StringComparer.OrdinalIgnoreCase))
                {
                    methods.Add(httpMethod);
                }
            }
        }

        return (methods is not null, methods ?? []);
    }

    private static bool MatchesPattern(RoutePattern pattern, string[] segments, ParameterPolicyFactory policyFactory)
    {
        if (segments.Length != pattern.PathSegments.Count)
        {
            return false;
        }

        for (var i = 0; i < segments.Length; i++)
        {
            if (!SegmentMatches(pattern.PathSegments[i], segments[i], policyFactory))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SegmentMatches(
        RoutePatternPathSegment segment,
        string value,
        ParameterPolicyFactory policyFactory)
    {
        if (segment.Parts.Count != 1)
        {
            return false;
        }

        return segment.Parts[0] switch
        {
            RoutePatternLiteralPart literal => string.Equals(literal.Content, value, StringComparison.OrdinalIgnoreCase),
            RoutePatternParameterPart parameter => value.Length > 0 && ConstraintsAllow(parameter, value, policyFactory),
            _ => false,
        };
    }

    private static bool ConstraintsAllow(
        RoutePatternParameterPart parameter,
        string value,
        ParameterPolicyFactory policyFactory)
    {
        if (parameter.ParameterPolicies.Count == 0)
        {
            return true;
        }

        var values = new RouteValueDictionary { [parameter.Name] = value };
        foreach (var reference in parameter.ParameterPolicies)
        {
            // Patterns from endpoint data sources carry unresolved policy names, and constraint
            // evaluation works without a request context for the constraints in use.
            var policy = reference.ParameterPolicy ?? policyFactory.Create(parameter, reference.Content);
            if (policy is IRouteConstraint constraint &&
                !constraint.Match(null, null, parameter.Name, values, RouteDirection.IncomingRequest))
            {
                return false;
            }
        }

        return true;
    }
}
