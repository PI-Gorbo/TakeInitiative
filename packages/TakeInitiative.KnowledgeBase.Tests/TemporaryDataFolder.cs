namespace TakeInitiative.KnowledgeBase.Tests;

/// <summary>
/// A throwaway folder shaped like a 5eTools <c>data/</c> directory, for the failure paths the
/// committed fixture cannot hold — a duplicate id, an empty bestiary, a folder that is not a 5eTools
/// checkout at all.
/// </summary>
internal sealed class TemporaryDataFolder : IDisposable
{
    private TemporaryDataFolder(string root) => Root = root;

    /// <summary>The folder to pass as <c>--from</c>.</summary>
    public string Root { get; }

    /// <summary>
    /// A checkout with a <c>data/bestiary/</c> holding the given monsters, an empty
    /// <c>data/spells/index.json</c>, and nothing else — no items, no books and no
    /// <c>package.json</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>data/</c> level is there so that the checkout the parse looks for a
    /// <c>package.json</c> in is this folder, rather than whatever the system's temp directory
    /// happens to hold.
    /// </para>
    /// <para>
    /// The empty spells index is there because a data folder without one is a failure, not an empty
    /// category: the bestiary's and the spells' <c>index.json</c> are both required, while
    /// <c>items.json</c>, <c>items-base.json</c>, <c>books.json</c> and <c>adventures.json</c> are
    /// optional. That is the Node script's behaviour, and <c>BuildGuardTests</c> pins it.
    /// </para>
    /// </remarks>
    public static TemporaryDataFolder WithMonsters(string monstersJson)
    {
        var root = Directory.CreateTempSubdirectory("ti-5etools-").FullName;
        var bestiary = Directory.CreateDirectory(Path.Combine(root, "data", "bestiary")).FullName;
        var spells = Directory.CreateDirectory(Path.Combine(root, "data", "spells")).FullName;

        File.WriteAllText(Path.Combine(bestiary, "index.json"), """{ "TST": "bestiary-tst.json" }""");
        File.WriteAllText(Path.Combine(bestiary, "bestiary-tst.json"), $"{{ \"monster\": [{monstersJson}] }}");
        File.WriteAllText(Path.Combine(spells, "index.json"), "{}");

        return new TemporaryDataFolder(root);
    }

    /// <summary>The <c>data/bestiary/</c> folder inside a <see cref="WithMonsters" /> checkout.</summary>
    public string Bestiary => Path.Combine(Root, "data", "bestiary");

    /// <summary>
    /// Writes one more file into the checkout, at a path relative to its root
    /// (<c>data/bestiary/fluff-index.json</c>), creating the folders it needs.
    /// </summary>
    /// <remarks>
    /// This is how 26g's artwork cases get a corpus with fluff images in it. They are not added to
    /// <c>Fixture/</c>: that folder is a byte copy of the corpus the Node script's golden output was
    /// taken from, and keeping it that way is what makes the golden file's one-line 26g diff
    /// readable as "a key was added" rather than "the inputs moved too".
    /// </remarks>
    public TemporaryDataFolder Write(string relativePath, string json)
    {
        var file = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, json);
        return this;
    }

    /// <summary>An empty folder, which is not a 5eTools checkout.</summary>
    public static TemporaryDataFolder Empty() =>
        new(Directory.CreateTempSubdirectory("ti-5etools-").FullName);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // Already gone; nothing to clean up.
        }
    }
}
