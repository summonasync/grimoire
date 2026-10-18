namespace Grimoire.Core;

public static class PersonaSectionExtensions
{
    public const string PersonaTag = "persona";

    public static string SectionTag(this PersonaSection section) => $"{PersonaTag}:{section.ToString().ToLowerInvariant()}";

    public static string Read(this PersonaDocument document, PersonaSection section) => section switch
    {
        PersonaSection.Identity => document.Identity,
        PersonaSection.Tone => document.Tone,
        PersonaSection.Preferences => document.Preferences,
        PersonaSection.Rules => document.Rules,
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown persona section."),
    };

    public static PersonaDocument WithSection(this PersonaDocument document, PersonaSection section, string content) => section switch
    {
        PersonaSection.Identity => document with { Identity = content },
        PersonaSection.Tone => document with { Tone = content },
        PersonaSection.Preferences => document with { Preferences = content },
        PersonaSection.Rules => document with { Rules = content },
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown persona section."),
    };

    public static IReadOnlyList<PersonaSection> Sections { get; } =
        [PersonaSection.Identity, PersonaSection.Tone, PersonaSection.Preferences, PersonaSection.Rules];
}
