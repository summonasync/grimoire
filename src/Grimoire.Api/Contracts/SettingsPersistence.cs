namespace Grimoire.Api.Contracts;

public sealed record SettingsPersistence(bool Available, string Reason, string Hint)
{
    public static SettingsPersistence Deferred { get; } = new(
        false,
        "settings_persistence_not_implemented",
        "Edit the Grimoire config file (~/.grimoire/config.json by default) and restart 'grimoire serve'.");
}
