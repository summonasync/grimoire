namespace Grimoire.Core;

/// <summary>
/// Outcome of reading text against the shared numeric grammar. Only <see cref="Finite"/> may
/// reach a value tree: a literal that parses yet is not finite (<c>NaN</c>, <c>Infinity</c>,
/// <c>1e999</c>) has to abort the caller before its arithmetic quietly substitutes something else.
/// </summary>
public enum NumberVerdict
{
    Finite,
    NotNumber,
    NonFinite,
}
