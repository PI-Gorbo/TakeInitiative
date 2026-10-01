using System.Globalization;
using System.Text.Json;

namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// A spell's label, ported from <c>build-5etools-index.mjs</c> lines 160-164.
/// </summary>
public static class Spells
{
    /// <summary>
    /// <c>Level 3 Evocation</c>, or <c>Illusion Cantrip</c> at level 0. Both halves come from
    /// 5eTools' own enum (a school letter) and a small integer, which is all a spell row needs;
    /// no spell has stats.
    /// </summary>
    /// <exception cref="FiveEToolsBuildException">
    /// The school is not one of the nine letters, or the level is not a whole number. Either means
    /// their format moved, and a spell with no level is not a spell we can label.
    /// </exception>
    public static string SpellLabel(JsonElement spell, string where)
    {
        var raw = Js.Get(spell, "school");
        if (Js.AsString(raw) is not { } letter || !Labels.Schools.TryGetValue(letter, out var school))
        {
            throw new FiveEToolsBuildException($"{where}: unknown school \"{Js.ToJsString(raw)}\"");
        }

        if (Js.AsInteger(Js.Get(spell, "level")) is not { } level)
        {
            throw new FiveEToolsBuildException($"{where}: level is not a number");
        }

        return level == 0
            ? $"{school} Cantrip"
            : $"Level {level.ToString(CultureInfo.InvariantCulture)} {school}";
    }
}
