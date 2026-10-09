using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Api.Features.Suggestions;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// Accepting a suggestion (23c.3): the one-span rule, and keeping the accepted mentions in step
/// with later edits (23c.4) and reverting them (23c.7).
/// </summary>
public class SuggestionEditTests
{
    private static readonly Guid Rellan = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Keep = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static string Link(string text, int start, int length, Guid id)
        => text[..start] + SuggestionEdit.MentionOf(text.Substring(start, length), id) + text[(start + length)..];

    [Fact]
    public void LinkingExactlyTheSpan_Passes()
    {
        const string text = "met rellan at the gates";
        SuggestionEdit.Check(text, Link(text, 4, 6, Rellan), 4, 6, Rellan).Should().BeNull();
        SuggestionEdit.Check(text, "met @[rellan](entry:11111111-1111-1111-1111-111111111111) at the gates", 4, 6, Rellan)
            .Should().BeNull();
    }

    [Fact]
    public void AnyOtherChange_IsRejected()
    {
        const string text = "met rellan at the gates";
        var linked = Link(text, 4, 6, Rellan);
        SuggestionEdit.Check(text, linked + "!", 4, 6, Rellan).Should().NotBeNull("the edit changed something else too");
        SuggestionEdit.Check(text, text, 4, 6, Rellan).Should().NotBeNull("nothing was linked");
        SuggestionEdit.Check(text, Link(text, 4, 6, Keep), 4, 6, Rellan).Should().NotBeNull("another entry was linked");
        SuggestionEdit.Check(text, Link(text, 4, 9, Rellan), 4, 6, Rellan).Should().NotBeNull("a longer span was linked");
    }

    [Fact]
    public void ASpanThatMoved_IsRejected()
    {
        const string text = "rellan met rellan";
        SuggestionEdit.Check(text, Link(text, 0, 6, Rellan), 11, 6, Rellan).Should().NotBeNull();
        SuggestionEdit.Check(text, Link(text, 11, 6, Rellan), 11, 6, Rellan).Should().BeNull();
    }

    [Fact]
    public void TwoSpansAtOnce_AreRejected()
    {
        const string text = "rellan at Greyhollow Keep";
        var both = Link(Link(text, 10, 15, Keep), 0, 6, Rellan);
        SuggestionEdit.Check(text, both, 0, 6, Rellan).Should().NotBeNull();
        SuggestionEdit.Check(text, both, 10, 15, Keep).Should().NotBeNull();
    }

    [Fact]
    public void Unicode_UsesUtf16Offsets_AndNeverSplitsACharacter()
    {
        const string text = "🐉 met Ærwyn";
        var start = text.IndexOf('Æ');
        SuggestionEdit.Check(text, Link(text, start, 5, Rellan), start, 5, Rellan).Should().BeNull();
        SuggestionEdit.Check(text, Link(text, 1, 5, Rellan), 1, 5, Rellan).Should().NotBeNull("the span starts inside the dragon");
    }

    [Fact]
    public void ASpanInsideAMention_ALinkOrCode_IsRejected()
    {
        var mention = SuggestionEdit.MentionOf("Rellan Ashvale", Keep);
        var inMention = $"met {mention}";
        var start = inMention.IndexOf("Rellan", StringComparison.Ordinal);
        SuggestionEdit.Check(inMention, Link(inMention, start, 6, Rellan), start, 6, Rellan).Should().NotBeNull();

        const string code = "run `rellan` now";
        SuggestionEdit.Check(code, Link(code, 5, 6, Rellan), 5, 6, Rellan).Should().NotBeNull();

        const string bracket = "met rel]lan";
        SuggestionEdit.Check(bracket, Link(bracket, 4, 7, Rellan), 4, 7, Rellan).Should().NotBeNull();
        SuggestionEdit.Check("met rellan", "met rellan", 8, 6, Rellan).Should().NotBeNull("the span runs past the text");
    }

    private static SessionNoteEdited Accept(string newText, int start, int length, Guid id, string version = "v1")
        => new(new Actor(Guid.NewGuid(), new ModelSuggestion("gliner", version, 0.87)), newText, false, null, new SuggestedSpan(start, length, id));

    [Fact]
    public void AnAcceptedSuggestion_IsARow_AndLaterEditsMoveIt()
    {
        const string text = "met rellan at the gates";
        var linked = Link(text, 4, 6, Rellan);
        var rows = SuggestedMentions.After(text, [], Accept(linked, 4, 6, Rellan));
        rows.Should().ContainSingle().Which.Should().Be(new SuggestedMention(Rellan, 4, "rellan", "gliner", "v1", 0.87));

        var prepended = "We " + linked;
        var moved = SuggestedMentions.Carry(linked, prepended, rows);
        moved.Single().Start.Should().Be(7);
        SuggestedMentions.Carry(prepended, prepended + " again", moved).Single().Start.Should().Be(7);
    }

    [Fact]
    public void AMentionEditedAway_OrRetyped_IsNoLongerARow()
    {
        const string text = "met rellan at the gates";
        var linked = Link(text, 4, 6, Rellan);
        var rows = SuggestedMentions.After(text, [], Accept(linked, 4, 6, Rellan));

        SuggestedMentions.Carry(linked, text, rows).Should().BeEmpty("the mention went back to text");
        SuggestedMentions.Carry(linked, linked.Replace("rellan", "Rellan"), rows).Should().BeEmpty("the mention's text changed");
    }

    [Fact]
    public void RowsInsideAChangedStretch_FollowTheirLiteral_WhenTheCountHolds()
    {
        var text = $"a {SuggestionEdit.MentionOf("rellan", Rellan)} b {SuggestionEdit.MentionOf("Keep", Keep)} c";
        var rows = new[]
        {
            new SuggestedMention(Rellan, 2, "rellan", "gliner", "v1", 0.9),
            new SuggestedMention(Keep, text.IndexOf("@[Keep", StringComparison.Ordinal), "Keep", "gliner", "v1", 0.8),
        };
        // Both ends change, so both rows sit in the changed stretch.
        var edited = "X" + text[1..^1] + "Y";
        SuggestedMentions.Carry(text, edited, rows).Should().Equal(rows);

        var withHandTyped = text.Replace(" b ", $" b {SuggestionEdit.MentionOf("rellan", Rellan)} ");
        SuggestedMentions.Carry(text, "X" + withHandTyped[1..], rows).Select(r => r.EntryId).Should().Equal(
            [Keep], "a second rellan appeared in the stretch, so which one was suggested is unknown");
    }

    [Fact]
    public void Revert_UnlinksOnlyTheRowsStillThere()
    {
        var hand = SuggestionEdit.MentionOf("rellan", Rellan);
        const string text = "met rellan, then rellan again";
        var linked = Link(text, 4, 6, Rellan);
        var rows = SuggestedMentions.After(text, [], Accept(linked, 4, 6, Rellan));
        var withHand = linked.Replace("then rellan", $"then {hand}");
        rows = SuggestedMentions.Carry(linked, withHand, rows);

        var (reverted, count) = PostSuggestionRevert.Revert(withHand, rows);
        count.Should().Be(1);
        reverted.Should().Be($"met rellan, then {hand} again", "the hand-typed mention stays");
        SuggestedMentions.Carry(withHand, reverted, rows).Should().BeEmpty();

        PostSuggestionRevert.Revert("met rellan", rows).Should().Be(("met rellan", 0), "a mention edited away is skipped");
    }
}
