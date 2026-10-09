using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// The handful of JavaScript semantics the ported script relies on, spelled out once so the
/// rest of the port can read like the original.
/// </summary>
/// <remarks>
/// <para>
/// 5eTools' files are arbitrary JSON and the Node script reaches into them with plain property
/// access, so the port works over <see cref="JsonElement"/> rather than deserialising into
/// types. The three distinctions that matter are the ones the script itself tests for:
/// <c>x !== undefined</c> (the property exists, even when its value is <c>null</c>),
/// truthiness (<c>!raw.hp</c>, <c>raw.srd52</c>), and <c>Number.isInteger</c>.
/// </para>
/// </remarks>
internal static class Js
{
    /// <summary>
    /// JavaScript property access: the element when the property exists — <c>null</c> for a
    /// missing property, which is how the script's <c>!== undefined</c> tests read. A property
    /// whose value is JSON <c>null</c> is present, and <c>_copy</c> inheritance depends on that.
    /// </summary>
    public static JsonElement? Get(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value
            : null;

    /// <inheritdoc cref="Get(JsonElement, string)" />
    public static JsonElement? Get(JsonElement? element, string name) =>
        element is { } value ? Get(value, name) : null;

    /// <summary><c>typeof x === "string"</c>, giving the string or <c>null</c>.</summary>
    public static string? AsString(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    /// <summary><c>typeof x === "object" &amp;&amp; x !== null</c>, arrays included.</summary>
    public static bool IsObjectOrArray(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object or JsonValueKind.Array };

    /// <summary><c>Array.isArray(x)</c>.</summary>
    public static bool IsArray(JsonElement? element) => element is { ValueKind: JsonValueKind.Array };

    /// <summary>
    /// <c>Number.isInteger(x)</c>, giving the value or <c>null</c>. A JSON number that is
    /// integral but outside <see cref="int"/> is reported as not an integer: every field read
    /// through here (page, AC, DEX, initiative, spell level) is a small count, and the index's
    /// row model stores them as <see cref="int"/>.
    /// </summary>
    public static int? AsInteger(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Number } value) return null;
        if (!value.TryGetDouble(out var number) || !double.IsInteger(number)) return null;
        return number is >= int.MinValue and <= int.MaxValue ? (int)number : null;
    }

    /// <summary>
    /// JavaScript truthiness. Absent, <c>null</c>, <c>false</c>, <c>0</c> and <c>""</c> are
    /// falsy; every object and array, including an empty one, is truthy.
    /// </summary>
    public static bool IsTruthy(JsonElement? element) => element?.ValueKind switch
    {
        null or JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.False => false,
        JsonValueKind.True => true,
        JsonValueKind.Number => element.Value.TryGetDouble(out var number) && number != 0 && !double.IsNaN(number),
        JsonValueKind.String => element.Value.GetString() is { Length: > 0 },
        _ => true,
    };

    /// <summary>
    /// <c>String(x)</c>, for the two places the script builds a string out of whatever it found:
    /// <c>keyOf</c>'s <c>_copy</c> lookup key and the messages that quote an unknown enum value.
    /// In real data these are always strings; the rest is here so a malformed file gives a
    /// readable message instead of a different one.
    /// </summary>
    public static string ToJsString(JsonElement? element) => element?.ValueKind switch
    {
        null or JsonValueKind.Undefined => "undefined",
        JsonValueKind.Null => "null",
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.String => element.Value.GetString() ?? "",
        JsonValueKind.Number => NumberToJsString(element.Value),
        JsonValueKind.Array => string.Join(",", element.Value.EnumerateArray().Select(item =>
            item.ValueKind is JsonValueKind.Null ? "" : ToJsString(item))),
        _ => "[object Object]",
    };

    private static string NumberToJsString(JsonElement element)
    {
        if (!element.TryGetDouble(out var number)) return element.GetRawText();
        return double.IsInteger(number) && Math.Abs(number) < 1e21
            ? ((long)number).ToString(CultureInfo.InvariantCulture)
            : number.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The characters JavaScript's <c>\s</c> matches, for <c>hpDiceOf</c>'s
    /// <c>formula.replace(/\s+/g, "")</c>. .NET's <c>\s</c> is a different set — it includes
    /// U+0085 and leaves out U+FEFF — so the set is spelled out rather than borrowed.
    /// </summary>
    public static bool IsJsWhiteSpace(char c) => c switch
    {
        '\t' or '\n' or '\r' or ' ' => true,
        (char)0x000b or (char)0x000c => true,                        // vertical tab, form feed
        (char)0x00a0 or (char)0x1680 => true,                        // no-break space, Ogham space mark
        >= (char)0x2000 and <= (char)0x200a => true,                 // en quad to hair space
        (char)0x2028 or (char)0x2029 => true,                        // line and paragraph separator
        (char)0x202f or (char)0x205f or (char)0x3000 => true,        // narrow, medium and ideographic space
        (char)0xfeff => true,                                        // byte order mark
        _ => false,
    };

    /// <summary>Strips every <see cref="IsJsWhiteSpace" /> character.</summary>
    public static string StripWhiteSpace(string value)
    {
        var stripped = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (!IsJsWhiteSpace(c)) stripped.Append(c);
        }

        return stripped.ToString();
    }
}
