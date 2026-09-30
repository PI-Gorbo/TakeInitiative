using FluentValidation;

namespace TakeInitiative.Api.Features.Suggestions;

/// <summary>
/// The suggestion a <c>PUT notes/{id}</c> accepts (23c.3): the model that proposed it, and the span
/// of the note's current text (<see cref="Start"/>, <see cref="Length"/>, UTF-16) that the edit
/// links to <see cref="EntryId"/>. The edit must be exactly that one link
/// (<see cref="SuggestionEdit.Check"/>). One suggestion per edit, so each has its own event and
/// confidence.
/// </summary>
public record SuggestionRequest
{
    /// <summary>The model's id, e.g. <c>gliner_small-v2.5</c>.</summary>
    public required string Model { get; init; }
    /// <summary>The model's exact version (its pinned weights), what revert matches on.</summary>
    public required string Version { get; init; }
    /// <summary>The model's confidence in the span, clamped to [0, 1].</summary>
    public required double Confidence { get; init; }
    public required int Start { get; init; }
    public required int Length { get; init; }
    /// <summary>The entry linked: an existing one the author can see, or the one entry in <c>newEntries</c>.</summary>
    public required Guid EntryId { get; init; }
}

public class SuggestionRequestValidator : AbstractValidator<SuggestionRequest>
{
    public SuggestionRequestValidator()
    {
        RuleFor(x => x.Model).NotEmpty().MaximumLength(SuggestionEdit.ModelMaxLength);
        RuleFor(x => x.Version).NotEmpty().MaximumLength(SuggestionEdit.VersionMaxLength);
        RuleFor(x => x.Start).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Length).InclusiveBetween(1, SuggestionEdit.SpanMaxLength);
        RuleFor(x => x.EntryId).NotEmpty();
    }
}

/// <summary>
/// The one-span rule for accepting a suggestion (23c.3, pure): provenance can only be attached to
/// an edit that links the suggested span and changes nothing else.
/// </summary>
public static class SuggestionEdit
{
    public const string ErrorKey = "suggestion";
    public const int ModelMaxLength = 80;
    public const int VersionMaxLength = 200;
    /// <summary>A span is at most this long, as in <c>POST suggestions/match</c>.</summary>
    public const int SpanMaxLength = 80;

    /// <summary>Characters a span cannot hold: they would break the mention's link text, or it is not one line of prose.</summary>
    private static readonly char[] NotInSpan = ['[', ']', '\\', '`', '\n', '\r'];

    /// <summary>The stored mention of <paramref name="entryId"/> showing <paramref name="text"/>.</summary>
    public static string MentionOf(string text, Guid entryId) => $"@[{text}](entry:{entryId:D})";

    /// <summary>
    /// Null when <paramref name="newText"/> is <paramref name="oldText"/> with exactly the span at
    /// <paramref name="start"/>, <paramref name="length"/> replaced by <c>@[span](entry:id)</c>, and
    /// that is a new mention (not inside a mention, a link or code) while every other mention stays;
    /// otherwise why not.
    /// </summary>
    public static string? Check(string oldText, string newText, int start, int length, Guid entryId)
    {
        var end = start + length;
        if (start < 0 || length < 1 || end > oldText.Length)
        {
            return "The suggested span is not in the note.";
        }
        if (char.IsLowSurrogate(oldText[start]) || (end < oldText.Length && char.IsLowSurrogate(oldText[end])))
        {
            return "The suggested span splits a character.";
        }
        var span = oldText.Substring(start, length);
        if (span.Trim().Length != span.Length || span.IndexOfAny(NotInSpan) >= 0)
        {
            return "The suggested span cannot be linked.";
        }

        var expected = string.Concat(oldText.AsSpan(0, start), MentionOf(span, entryId), oldText.AsSpan(end));
        if (!string.Equals(expected, newText, StringComparison.Ordinal))
        {
            return "Accepting a suggestion must link exactly its span and change nothing else.";
        }

        var before = MentionParser.Parse(oldText);
        var after = MentionParser.Parse(newText);
        var added = after.Count == before.Count + 1
            && Enumerable.Range(0, after.Count).Any(i =>
                after[i].EntryId == entryId && after.Where((_, j) => j != i).SequenceEqual(before));
        return added ? null : "The suggested span is not plain text: it is inside a mention, a link or code.";
    }
}
