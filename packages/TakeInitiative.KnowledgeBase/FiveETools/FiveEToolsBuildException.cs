namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// The port of <c>build-5etools-index.mjs</c>'s <c>BuildError</c>: a parse that cannot be
/// trusted. The CLI (26c) turns this into exit code 1 and writes nothing.
/// </summary>
public sealed class FiveEToolsBuildException(string message) : Exception(message);
