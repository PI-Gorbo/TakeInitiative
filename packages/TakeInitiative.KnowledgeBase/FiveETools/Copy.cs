using System.Text.Json;

namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// A monster's stat-block fields after <c>_copy</c> inheritance, keyed by
/// <see cref="Copy.Fields" />.
/// </summary>
/// <remarks>
/// A resolved monster is deliberately not a 5eTools object. It is the seven named fields the index
/// is allowed to look at and nothing else, so no amount of later editing can make a label or a
/// stat reach into rules text that happened to be on the same row.
/// </remarks>
public sealed class MonsterFields
{
    private readonly Dictionary<string, JsonElement> fields;

    internal MonsterFields(Dictionary<string, JsonElement> fields) => this.fields = fields;

    /// <summary>
    /// The named field, or <c>null</c> when the monster does not have it. A field explicitly set
    /// to JSON <c>null</c> is present and comes back as a null-kind element, which is what
    /// JavaScript's <c>!== undefined</c> test distinguishes.
    /// </summary>
    public JsonElement? this[string name] => fields.TryGetValue(name, out var value) ? value : null;

    /// <summary>Everything in <see cref="Copy.Fields" /> that this monster carries itself.</summary>
    public static MonsterFields From(JsonElement monster)
    {
        var own = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var field in Copy.Fields)
        {
            if (Js.Get(monster, field) is { } value) own[field] = value;
        }

        return new MonsterFields(own);
    }
}

/// <summary>
/// 5eTools' <c>_copy</c> inheritance, ported from <c>build-5etools-index.mjs</c> lines 154-167 and
/// 254-267. This is the part of their format that is least guessable from the data, and the part
/// the golden-file test exists to protect.
/// </summary>
/// <remarks>
/// <para>
/// A great many monsters in 5eTools' bestiary are not written out. They carry a <c>_copy</c>
/// naming a base creature in some other file and then only the handful of fields that differ —
/// Test Gremlin Chief is a Test Gremlin with more hit points and a higher CR. Reading such a row
/// on its own gives a monster with no size, no type, no AC and no HP.
/// </para>
/// <para>
/// Resolution is three rules, in this order:
/// </para>
/// <list type="number">
///   <item>
///     A row with no <c>_copy</c> resolves to itself. It is already complete.
///   </item>
///   <item>
///     A row with a <c>_copy</c> resolves its base <em>first</em>, recursively, because a base may
///     itself be a copy. The recursion carries the set of keys already visited, so a cycle — a row
///     copying itself, or two rows copying each other — resolves to nothing rather than
///     overflowing the stack. A base that is not in the bestiary at all resolves to nothing too:
///     that is the orphan case, and the row is skipped and reported rather than emitted half
///     filled.
///   </item>
///   <item>
///     The base's fields are laid down first and the copy's own fields are laid over them, so a
///     field the copy states wins and a field it is silent about is inherited. "Silent" means the
///     property is absent, not falsy: a copy that sets <c>"ac": null</c> has stated it.
///   </item>
/// </list>
/// <para>
/// Two things a <c>_copy</c> can carry are deliberately not honoured. <c>_mod</c> is a list of
/// edits to traits, actions and entries — it only ever changes text the index does not store, so
/// applying it would be work with no output. <c>_templates</c> layers a creature template (a
/// zombie, a half-dragon) over the base; the fields it would change are again mostly text, and
/// getting it wrong would silently mislabel a monster, so the script counts these rows and
/// inherits the base plainly. Both decisions are visible in the report.
/// </para>
/// </remarks>
public static class Copy
{
    /// <summary>
    /// The fields a copy may take from its base — only what a label or a stat needs. The list is
    /// the port's answer to "what does <c>_copy</c> inherit?": everything else on a 5eTools
    /// monster is content, and the index never reads it, so it never has to be merged.
    /// </summary>
    public static readonly string[] Fields = ["size", "type", "ac", "hp", "cr", "dex", "initiative"];

    /// <summary>
    /// The lookup key for a name and source pair: both lower-cased and joined with <c>|</c>.
    /// Line 210's <c>keyOf</c>, including its <c>String(...)</c> coercion — a <c>_copy</c> with no
    /// <c>name</c> looks for the base called <c>"undefined"</c>, finds nothing, and its row is
    /// reported as an orphan.
    /// </summary>
    public static string KeyOf(JsonElement? name, JsonElement? source) =>
        $"{Js.ToJsString(name).ToLowerInvariant()}|{Js.ToJsString(source).ToLowerInvariant()}";

    /// <inheritdoc cref="KeyOf(JsonElement?, JsonElement?)" />
    public static string KeyOf(string name, string source) =>
        $"{name.ToLowerInvariant()}|{source.ToLowerInvariant()}";

    /// <summary>
    /// Resolves one monster against the bestiary, keyed by <see cref="KeyOf(string, string)" />.
    /// </summary>
    /// <returns>
    /// The resolved fields, or <c>null</c> when the chain cannot be followed — a missing base or a
    /// cycle. The caller reports a <c>null</c> as a skipped orphan.
    /// </returns>
    public static MonsterFields? Resolve(JsonElement monster, IReadOnlyDictionary<string, JsonElement> byKey) =>
        Resolve(monster, byKey, new HashSet<string>(StringComparer.Ordinal));

    private static MonsterFields? Resolve(
        JsonElement monster,
        IReadOnlyDictionary<string, JsonElement> byKey,
        HashSet<string> seen)
    {
        var copy = Js.Get(monster, "_copy");
        if (!Js.IsTruthy(copy)) return MonsterFields.From(monster);

        var key = KeyOf(Js.Get(copy, "name"), Js.Get(copy, "source"));
        if (!seen.Add(key)) return null;
        if (!byKey.TryGetValue(key, out var found)) return null;

        var parent = Resolve(found, byKey, seen);
        if (parent is null) return null;

        var merged = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var field in Fields)
        {
            if (parent[field] is { } inherited) merged[field] = inherited;
        }

        foreach (var field in Fields)
        {
            if (Js.Get(monster, field) is { } own) merged[field] = own;
        }

        return new MonsterFields(merged);
    }
}
