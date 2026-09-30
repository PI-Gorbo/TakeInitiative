using System.Text.Json;

namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// The file reading half of the script's <c>readJson</c> / <c>readOptionalJson</c> pair, plus the
/// ownership every <see cref="JsonElement" /> in the parse depends on.
/// </summary>
/// <remarks>
/// A <see cref="JsonElement" /> is a window onto its <see cref="JsonDocument" /> and stops working
/// the moment that document is disposed. The parse holds elements from every file it has read —
/// the bestiary's <c>_copy</c> lookup is a dictionary of them — so the documents have to outlive
/// the whole build. This class keeps them and the build disposes it once, at the end, by which
/// point the index holds nothing but strings and numbers.
/// </remarks>
/// <param name="onFile">
/// Called with each file's path as it is read, for the CLI's progress line (26c). Every data file
/// the parse touches goes through this class, so this is the one place a caller has to hook to see
/// them all; nothing about the parse depends on it, and it is null in every test but the one that
/// asserts it fires.
/// </param>
internal sealed class JsonReader(Action<string>? onFile = null) : IDisposable
{
    // 5eTools' files are wide rather than deep, but the default 64 is a limit Node's JSON.parse
    // does not have, and a port should not fail on a file the script would read.
    private static readonly JsonDocumentOptions Options = new() { MaxDepth = 1024 };

    private readonly List<JsonDocument> documents = [];

    /// <exception cref="FiveEToolsBuildException">The file is missing or is not JSON.</exception>
    public JsonElement Read(string file)
    {
        onFile?.Invoke(file);

        try
        {
            var document = JsonDocument.Parse(File.ReadAllBytes(file), Options);
            documents.Add(document);
            return document.RootElement;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
                                          or JsonException or ArgumentException or NotSupportedException)
        {
            throw new FiveEToolsBuildException($"cannot read {file}: {error.Message}");
        }
    }

    /// <summary><c>null</c> when the file is not there; a build error when it is there and broken.</summary>
    public JsonElement? ReadOptional(string file) => Path.Exists(file) ? Read(file) : null;

    /// <summary>
    /// <c>json[key] ?? []</c>. A key that is there but is not a list is a build error: the script
    /// would crash on it, and crashing quietly through a half-written index is worse.
    /// </summary>
    public static IEnumerable<JsonElement> ArrayOrEmpty(JsonElement document, string key, string file)
    {
        if (Js.Get(document, key) is not { } value || value.ValueKind == JsonValueKind.Null) return [];
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new FiveEToolsBuildException($"cannot read {file}: \"{key}\" is not a list");
        }

        return value.EnumerateArray();
    }

    /// <inheritdoc cref="ArrayOrEmpty(JsonElement, string, string)" />
    public static IEnumerable<JsonElement> ArrayOrEmpty(JsonElement? document, string key, string file) =>
        document is { } value ? ArrayOrEmpty(value, key, file) : [];

    public void Dispose()
    {
        foreach (var document in documents) document.Dispose();
        documents.Clear();
    }
}
