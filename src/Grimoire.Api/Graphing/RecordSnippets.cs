namespace Grimoire.Api.Graphing;

public static class RecordSnippets
{
    private const int MaxLength = 120;

    public static string Of(string content)
    {
        var flattened = string.Join(
            ' ',
            content.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

        return flattened.Length <= MaxLength ? flattened : flattened[..MaxLength] + "...";
    }
}
