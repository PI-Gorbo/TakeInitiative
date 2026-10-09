using System.Text;
using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Suggestions;

public record PostSuggestionRevertRequest
{
    public Guid CampaignId { get; init; }
    public required string Model { get; init; }
    public required string Version { get; init; }
}

public record PostSuggestionRevertResponse
{
    /// <summary>How many of the caller's notes were edited.</summary>
    public required int Notes { get; init; }
    /// <summary>How many mentions went back to plain text.</summary>
    public required int Mentions { get; init; }
    /// <summary>Entries the caller created from this version's suggestions. They are kept: there is no entry delete.</summary>
    public required EntrySummaryResponse[] CreatedEntries { get; init; }
}

public class PostSuggestionRevertRequestValidator : Validator<PostSuggestionRevertRequest>
{
    public PostSuggestionRevertRequestValidator()
    {
        RuleFor(x => x.Model).NotEmpty().MaximumLength(SuggestionEdit.ModelMaxLength);
        RuleFor(x => x.Version).NotEmpty().MaximumLength(SuggestionEdit.VersionMaxLength);
    }
}

/// <summary>
/// Reverts one model version's accepted suggestions in the caller's own notes (23c.7). Each mention
/// in <see cref="SessionNote.SuggestedMentions"/> from that model and version that is still there
/// as accepted goes back to its plain text, and each note changed gets one
/// <see cref="SessionNoteEdited"/> by the author alone: the revert is the author's own act. Mentions
/// the author typed by hand, even of the same entry, are untouched, and so is every other member's
/// note (invariant 4: a DM cannot revert a player's suggestions). Entries created from the
/// suggestions stay and are listed.
/// </summary>
public class PostSuggestionRevert(IDocumentSession session, IHubContext<CampaignHub> hub)
    : Endpoint<PostSuggestionRevertRequest, PostSuggestionRevertResponse>
{
    public override void Configure()
    {
        Post("/api/campaigns/{CampaignId}/suggestions/revert");
    }

    public override async Task HandleAsync(PostSuggestionRevertRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var model = req.Model.Trim();
        var version = req.Version.Trim();
        bool From(string m, string v) => m == model && v == version;

        var edited = new List<Guid>();
        var mentions = 0;
        foreach (var note in await SuggestionNotes.OfAuthor(session, req.CampaignId, member, ct))
        {
            var (text, count) = Revert(note.Text, note.SuggestedMentions.Where(r => From(r.Model, r.Version)));
            if (count == 0)
            {
                continue;
            }
            session.Events.Append(note.Id, new SessionNoteEdited(Actor.Member(member.MemberId), text, note.IsRecap));
            edited.Add(note.Id);
            mentions += count;
        }
        if (edited.Count > 0)
        {
            await session.SaveChangesAsync(ct);
        }

        var created = await CreatedEntries(req.CampaignId, member, From, ct);

        if (edited.Count > 0)
        {
            foreach (var note in await session.LoadManyAsync<SessionNote>(ct, edited))
            {
                await hub.NotifySessionNoteUpserted(note);
            }
        }

        await SendAsync(new PostSuggestionRevertResponse
        {
            Notes = edited.Count,
            Mentions = mentions,
            CreatedEntries = [.. created.Select(EntrySummaryResponse.From)],
        }, cancellation: ct);
    }

    /// <summary>
    /// The entries <paramref name="member"/> created from this model version's suggestions and can
    /// still see: their own <see cref="EntryCreated"/> events carry the model.
    /// </summary>
    private async Task<IReadOnlyList<Entry>> CreatedEntries(Guid campaignId, Member member, Func<string, string, bool> from, CancellationToken ct)
    {
        var memberId = member.MemberId;
        var candidates = await session.Query<Entry>()
            .Listed(campaignId, member)
            .Where(e => e.CreatorMemberId == memberId && e.CreatedFromNoteId != null)
            .ToListAsync(ct);
        if (candidates.Count == 0)
        {
            return [];
        }
        // A List, not an array: Marten cannot translate C# 14's span-based Contains on an array.
        var ids = candidates.Select(e => e.Id).ToList();
        var fromModel = (await session.Events.QueryAllRawEvents()
                .Where(e => ids.Contains(e.StreamId))
                .ToListAsync(ct))
            .Where(e => e.Data is EntryCreated { Actor.Model: { } m } && from(m.Name, m.Version))
            .Select(e => e.StreamId)
            .ToHashSet();
        return candidates
            .Where(e => fromModel.Contains(e.Id))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// <paramref name="text"/> with each of <paramref name="rows"/>' mentions back to its plain text,
    /// where the mention is still there as accepted; and how many were.
    /// </summary>
    public static (string Text, int Count) Revert(string text, IEnumerable<SuggestedMention> rows)
    {
        var builder = new StringBuilder(text);
        var count = 0;
        foreach (var row in rows.OrderByDescending(r => r.Start))
        {
            var literal = row.Literal;
            if (row.Start >= 0 && row.Start + literal.Length <= text.Length
                && string.CompareOrdinal(text, row.Start, literal, 0, literal.Length) == 0)
            {
                builder.Remove(row.Start, literal.Length).Insert(row.Start, row.Text);
                count++;
            }
        }
        return (builder.ToString(), count);
    }
}
