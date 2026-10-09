namespace TakeInitiative.Api.Features.Suggestions;

/// <summary>
/// A mention in a note that the author made by accepting a suggestion (23c.4), kept by the
/// <see cref="SessionNote"/> projection in <see cref="SessionNote.SuggestedMentions"/>. The
/// mention is the literal <see cref="Literal"/> (<c>@[Text](entry:EntryId)</c>) at
/// <see cref="Start"/> (UTF-16) of the note's text. "Human-asserted only" and revert read these
/// rows, so neither replays events. A mention the author typed by hand is never a row.
/// </summary>
public sealed record SuggestedMention(Guid EntryId, int Start, string Text, string Model, string Version, double Confidence)
{
    /// <summary>The mention as the note's text holds it.</summary>
    public string Literal => SuggestionEdit.MentionOf(Text, EntryId);
}

/// <summary>
/// Where the suggestion an edit accepted sits (on <see cref="SessionNoteEdited.Suggestion"/>): the
/// span at <see cref="Start"/>, <see cref="Length"/> characters of the text before the edit, now
/// the mention of <see cref="EntryId"/> starting at the same offset. The model itself is on the
/// event's <see cref="Actor.Model"/>.
/// </summary>
public sealed record SuggestedSpan(int Start, int Length, Guid EntryId);

/// <summary>
/// Keeping <see cref="SessionNote.SuggestedMentions"/> in step with the text (pure). An edit is
/// read as its common prefix and suffix with the text before it, and the changed stretch between:
/// <list type="bullet">
/// <item>a row wholly in the prefix stays where it is, and one wholly in the suffix moves by the
/// change in length;</item>
/// <item>rows in the changed stretch are matched to the same literal in the new stretch in order,
/// but only when the stretch has as many of that literal before and after. Otherwise the edit
/// added or removed one of them, it cannot tell which, and those rows are dropped: the mentions
/// then count as the author's own.</item>
/// </list>
/// A dropped row never comes back. A revert (23c.7) turns its mentions back into plain text, so
/// its rows leave by the second rule.
/// </summary>
public static class SuggestedMentions
{
    public static SuggestedMention[] Carry(string oldText, string newText, IReadOnlyList<SuggestedMention>? rows)
    {
        if (rows is null || rows.Count == 0)
        {
            return [];
        }
        if (oldText == newText)
        {
            return [.. rows];
        }

        var prefix = 0;
        var most = Math.Min(oldText.Length, newText.Length);
        while (prefix < most && oldText[prefix] == newText[prefix])
        {
            prefix++;
        }
        var suffix = 0;
        while (suffix < most - prefix && oldText[^(suffix + 1)] == newText[^(suffix + 1)])
        {
            suffix++;
        }
        var oldEnd = oldText.Length - suffix;
        var newEnd = newText.Length - suffix;
        var delta = newText.Length - oldText.Length;

        var kept = new List<SuggestedMention>();
        var changed = new List<SuggestedMention>();
        foreach (var row in rows)
        {
            var end = row.Start + row.Literal.Length;
            if (end <= prefix)
            {
                kept.Add(row);
            }
            else if (row.Start >= oldEnd)
            {
                kept.Add(row with { Start = row.Start + delta });
            }
            else
            {
                changed.Add(row);
            }
        }

        foreach (var group in changed.GroupBy(r => r.Literal))
        {
            var before = Occurrences(oldText, group.Key, prefix, oldEnd);
            var after = Occurrences(newText, group.Key, prefix, newEnd);
            if (before.Count != after.Count)
            {
                continue;
            }
            foreach (var row in group)
            {
                var index = before.IndexOf(row.Start);
                if (index >= 0)
                {
                    kept.Add(row with { Start = after[index] });
                }
            }
        }

        return [.. kept
            .Where(r => r.Start >= 0 && r.Start + r.Literal.Length <= newText.Length
                && string.CompareOrdinal(newText, r.Start, r.Literal, 0, r.Literal.Length) == 0)
            .DistinctBy(r => r.Start)
            .OrderBy(r => r.Start)];
    }

    /// <summary>A row for the suggestion <paramref name="edit"/> accepted, if it has one and its mention is in the text.</summary>
    public static SuggestedMention? Accepted(SessionNoteEdited edit)
    {
        if (edit.Actor.Model is not { } model || edit.Suggestion is not { } span)
        {
            return null;
        }
        var textStart = span.Start + 2;
        if (span.Start < 0 || span.Length < 1 || textStart + span.Length > edit.Text.Length)
        {
            return null;
        }
        var row = new SuggestedMention(
            span.EntryId, span.Start, edit.Text.Substring(textStart, span.Length), model.Name, model.Version, model.Confidence);
        return edit.Text.AsSpan(span.Start).StartsWith(row.Literal, StringComparison.Ordinal) ? row : null;
    }

    /// <summary>The rows after <paramref name="edit"/>: the old ones carried over, plus the one it accepted.</summary>
    public static SuggestedMention[] After(string oldText, IReadOnlyList<SuggestedMention>? rows, SessionNoteEdited edit)
    {
        var carried = Carry(oldText, edit.Text, rows);
        return Accepted(edit) is { } row
            ? [.. carried.Where(r => r.Start != row.Start).Append(row).OrderBy(r => r.Start)]
            : carried;
    }

    /// <summary>The starts of <paramref name="literal"/> in <paramref name="text"/> that overlap [from, to).</summary>
    private static List<int> Occurrences(string text, string literal, int from, int to)
    {
        var found = new List<int>();
        var at = Math.Max(0, from - literal.Length + 1);
        while ((at = text.IndexOf(literal, at, StringComparison.Ordinal)) >= 0 && at < to)
        {
            if (at + literal.Length > from)
            {
                found.Add(at);
            }
            at++;
        }
        return found;
    }
}
