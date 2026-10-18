namespace Grimoire.Cli;

internal static class ExitCodes
{
    public const int Ok = 0;

    public const int Runtime = 1;

    public const int Usage = 2;

    public const int Config = 3;

    public const int Dependency = 4;

    public const int NotFound = 5;

    public const int UpstreamUnavailable = 6;

    public const int ReindexRequired = 7;
}
