using Grimoire.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli;

internal static class BriefBudget
{
    // --max-chars is the one knob the CLI holds over Core's own budget walk, so it overrides the option
    // the walk reads instead of trimming anything afterwards: the omitted list, the digest and the
    // section markers all stay Core's answer for the smaller budget. The daily purse rides the same
    // envelope (min, never looser than config): the owner ruling from review 046-05 that --max-chars
    // must not leave a 6000-char daily flood behind.
    public static BriefService WithMaxTotalChars(IServiceProvider services, int maxTotalChars)
    {
        var options = services.GetRequiredService<MemoryEngineOptions>();
        return new BriefService(
            services.GetRequiredService<IMemoryStore>(),
            services.GetRequiredService<PersonaService>(),
            services.GetRequiredService<ITomeManifestStore>(),
            services.GetRequiredService<IClock>(),
            options with
            {
                BriefMaxTotalChars = maxTotalChars,
                BriefDailyMaxTotalChars = Math.Min(maxTotalChars, options.BriefDailyMaxTotalChars),
            });
    }
}
