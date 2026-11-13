using Grimoire.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli;

internal static class BriefBudget
{
    // --max-chars is the one knob the CLI holds over Core's own budget walk, so it overrides the option
    // the walk reads instead of trimming anything afterwards: the omitted list, the digest and the
    // section markers all stay Core's answer for the smaller budget.
    public static BriefService WithMaxTotalChars(IServiceProvider services, int maxTotalChars) => new(
        services.GetRequiredService<IMemoryStore>(),
        services.GetRequiredService<PersonaService>(),
        services.GetRequiredService<ITomeManifestStore>(),
        services.GetRequiredService<IClock>(),
        services.GetRequiredService<MemoryEngineOptions>() with { BriefMaxTotalChars = maxTotalChars });
}
