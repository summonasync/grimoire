namespace Grimoire.Core;

public sealed class ProjectRegistryStaleException(string message) : Exception(message);
