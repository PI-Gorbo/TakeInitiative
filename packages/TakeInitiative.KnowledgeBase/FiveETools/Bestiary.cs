using System.Text.Json;
using System.Text.RegularExpressions;

namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// What a monster row is allowed to carry: a challenge rating, a size, a creature type, an AC, a
/// hit-point dice expression and an initiative bonus. Ported from
/// <c>build-5etools-index.mjs</c> lines 95-152.
/// </summary>
/// <remarks>
/// <para>
/// Every reader here is total: a shape it does not recognise gives <c>null</c>, and a monster with
/// a <c>null</c> anywhere in AC, HP or initiative gets no <c>stats</c> object at all rather than a
/// partial one. That matters because these three numbers are what step 22's encounter builder will
/// roll initiative from, and half a stat block is worse than none.
/// </para>
/// </remarks>
public static partial class Bestiary
{
    /// <summary>
    /// <c>^\d+d\d+([+-]\d+)?$</c> from lines 118 and 372. Spelled with <c>[0-9]</c> because .NET's
    /// <c>\d</c> matches every Unicode decimal digit while JavaScript's is ASCII, and with
    /// <c>\z</c> because .NET's <c>$</c> also matches before a trailing newline.
    /// </summary>
    [GeneratedRegex(@"^[0-9]+d[0-9]+([+-][0-9]+)?\z", RegexOptions.CultureInvariant)]
    public static partial Regex HpDiceShape();

    /// <summary>
    /// The challenge rating, as the string a label shows. A monster's <c>cr</c> is either that
    /// string or an object whose <c>cr</c> holds it alongside lair and coven variants, which are
    /// ignored.
    /// </summary>
    public static string? CrOf(JsonElement? raw)
    {
        var value = Js.IsObjectOrArray(raw) ? Js.Get(raw, "cr") : raw;
        var text = Js.AsString(value);
        return text is not null && Labels.ChallengeRatings.Contains(text) ? text : null;
    }

    /// <summary>
    /// The proficiency bonus a challenge rating implies, for initiative. A fractional CR is
    /// always +2, and so is everything up to CR 4.
    /// </summary>
    public static int ProficiencyBonus(string cr)
    {
        if (cr.Contains('/', StringComparison.Ordinal)) return 2;
        var number = int.Parse(cr, System.Globalization.CultureInfo.InvariantCulture);
        return Math.Max(2, 2 + (int)Math.Ceiling((number - 4) / 4.0));
    }

    /// <summary>
    /// The armour class. 5eTools writes <c>ac</c> as an array whose entries are either a plain
    /// number or an object — <c>{ ac, from }</c> for the source of the armour, <c>{ ac, condition }</c>
    /// for a conditional value such as "with shield", and <c>{ special }</c> for one that cannot be
    /// stated as a number at all ("13 + the creature's proficiency bonus"). Only the first entry is
    /// read, because the later ones are alternatives rather than a total, and a <c>special</c>
    /// entry gives no AC.
    /// </summary>
    public static int? AcOf(JsonElement? raw)
    {
        if (raw is not { ValueKind: JsonValueKind.Array } entries || entries.GetArrayLength() == 0) return null;

        var first = entries[0];
        if (Js.AsInteger(first) is { } number) return number;
        return Js.IsObjectOrArray(first) ? Js.AsInteger(Js.Get(first, "ac")) : null;
    }

    /// <summary>
    /// The hit-point dice, normalised to <c>NdM</c>, <c>NdM+K</c> or <c>NdM-K</c> with no spaces.
    /// Anything the index cannot roll — a <c>{ special }</c> hit-point block, or a formula with
    /// more than one modifier — gives <c>null</c>.
    /// </summary>
    public static string? HpDiceOf(JsonElement? raw)
    {
        if (Js.AsString(Js.Get(raw, "formula")) is not { } formula) return null;
        var dice = Js.StripWhiteSpace(formula);
        return HpDiceShape().IsMatch(dice) ? dice : null;
    }

    /// <summary>
    /// The initiative bonus, in the three forms 5eTools uses: an explicit
    /// <c>initiative.initiative</c> (the 2024 monsters), a DEX modifier, or a DEX modifier plus a
    /// multiple of the proficiency bonus the challenge rating implies. The last of those needs a CR
    /// it recognises, so a monster with proficiency and an odd CR has no initiative bonus, and
    /// therefore no stats.
    /// </summary>
    public static int? InitiativeOf(MonsterFields monster)
    {
        var init = monster["initiative"];
        if (Js.IsObjectOrArray(init) && Js.AsInteger(Js.Get(init, "initiative")) is { } stated) return stated;

        if (Js.AsInteger(monster["dex"]) is not { } dex) return null;
        var dexMod = (int)Math.Floor((dex - 10) / 2.0);

        var proficiency = Js.IsObjectOrArray(init) ? Js.AsInteger(Js.Get(init, "proficiency")) ?? 0 : 0;
        if (proficiency == 0) return dexMod;

        var cr = CrOf(monster["cr"]);
        return cr is null ? null : dexMod + (proficiency * ProficiencyBonus(cr));
    }

    /// <summary>
    /// The muted line under a monster's name: <c>CR 13 · Large Aberration</c>. Either half may be
    /// missing, and a monster with neither gets no label.
    /// </summary>
    public static string? MonsterLabel(MonsterFields monster, string where)
    {
        var cr = CrOf(monster["cr"]);
        var kind = Join(" ", SizeLabel(monster["size"], where), TypeLabel(monster["type"]));
        var label = Join(" · ", cr is null ? "" : $"CR {cr}", kind);
        return label.Length == 0 ? null : label;
    }

    /// <summary>
    /// The size, as one or more words. A monster may be two sizes ("Large or Huge"), and a letter
    /// that is not a size fails the build rather than being dropped: an unknown size letter means
    /// 5eTools has changed their enum, and the right response is to look rather than to ship rows
    /// missing half a label.
    /// </summary>
    private static string SizeLabel(JsonElement? size, string where)
    {
        // Array.isArray(size) ? size : size == null ? [] : [size] — `== null` catches both a
        // missing field and an explicit JSON null.
        IEnumerable<JsonElement> letters = [];
        if (size is { } value && value.ValueKind != JsonValueKind.Null)
        {
            if (value.ValueKind == JsonValueKind.Array) letters = value.EnumerateArray();
            else letters = [value];
        }

        var words = new List<string>();
        foreach (var letter in letters)
        {
            var text = Js.AsString(letter);
            if (text is null || !Labels.Sizes.TryGetValue(text, out var word))
            {
                throw new FiveEToolsBuildException($"{where}: unknown size \"{Js.ToJsString(letter)}\"");
            }

            words.Add(word);
        }

        return string.Join(" or ", words);
    }

    /// <summary>
    /// The creature type. <c>type</c> is a bare string, or <c>{ type, tags, swarmSize }</c>, or
    /// <c>{ type: { choose: [...] } }</c> for a creature that is one of several. Only the enum is
    /// read: <c>tags</c> and every other neighbouring field is free text 5eTools wrote, so it is
    /// dropped, and a word that is not one of the fourteen creature types is dropped with it.
    /// </summary>
    private static string TypeLabel(JsonElement? type)
    {
        var value = Js.IsObjectOrArray(type) ? Js.Get(type, "type") : type;

        // { type: { choose: [...] } } is the only nested shape; anything else is the one word.
        IEnumerable<JsonElement> words = [];
        if (Js.IsObjectOrArray(value) && Js.Get(value, "choose") is { ValueKind: JsonValueKind.Array } choose)
        {
            words = choose.EnumerateArray();
        }
        else if (value is { } single)
        {
            words = [single];
        }

        var known = new List<string>();
        foreach (var word in words)
        {
            if (Js.AsString(word) is { } text && Labels.Types.Contains(text.ToLowerInvariant()))
            {
                known.Add(Labels.TitleCase(text.ToLowerInvariant()));
            }
        }

        return string.Join(" or ", known);
    }

    /// <summary>
    /// <c>[a, b].filter(Boolean).join(separator)</c>: empty parts leave no separator behind.
    /// </summary>
    private static string Join(string separator, params string[] parts) =>
        string.Join(separator, parts.Where(part => part.Length > 0));
}
