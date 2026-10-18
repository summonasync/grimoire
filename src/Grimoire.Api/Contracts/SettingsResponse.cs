namespace Grimoire.Api.Contracts;

public sealed record SettingsResponse(
    EngineSettings Memory,
    ServeSettings Serve,
    SettingsPersistence Persistence,
    IReadOnlyList<string> Editable);
