using System.Text.Json;

namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// An item's label, ported from <c>build-5etools-index.mjs</c> lines 166-171 and 306-314.
/// </summary>
public static class Items
{
    /// <summary>
    /// The three fields an item's label is built from. An item's <c>_copy</c> is a shallow
    /// override — <c>{ ...base, ...own }</c> at line 312 — so a field the copy states wins and one
    /// it does not mention is inherited. "States" means the property is there, null included.
    /// </summary>
    public static JsonElement? Field(JsonElement item, JsonElement? baseItem, string name) =>
        Js.Get(item, name) ?? Js.Get(baseItem, name);

    /// <summary>
    /// <c>Uncommon Wondrous Item</c>, <c>Rare Melee Weapon</c>, or just <c>Melee Weapon</c> for a
    /// mundane one. Both halves come from an enum: a rarity word 5eTools uses as a key, and the
    /// type code in front of the <c>|</c> in <c>"M|XPHB"</c>. An unknown code contributes nothing
    /// rather than leaking their text, and an item with neither gets no label.
    /// </summary>
    public static string? ItemLabel(JsonElement item, JsonElement? baseItem)
    {
        var rarity = Js.AsString(Field(item, baseItem, "rarity")) is { } word
                     && Labels.Rarities.TryGetValue(word.ToLowerInvariant(), out var mapped)
            ? mapped
            : "";

        // `wondrous: true` wins over the type code: a wondrous item's code, where it has one, says
        // how it is worn rather than what it is.
        var type = "";
        if (Field(item, baseItem, "wondrous") is { ValueKind: JsonValueKind.True })
        {
            type = "Wondrous Item";
        }
        else if (Js.AsString(Field(item, baseItem, "type")) is { } code
                 && Labels.ItemTypes.TryGetValue(code.Split('|')[0], out var named))
        {
            type = named;
        }

        var label = string.Join(" ", new[] { rarity, type }.Where(part => part.Length > 0));
        return label.Length == 0 ? null : label;
    }
}
