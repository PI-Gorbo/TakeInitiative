using FastEndpoints;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Suggestions;

public record GetSuggestionModelsRequest
{
    public Guid CampaignId { get; init; }
}

public record GetSuggestionModelsResponse
{
    /// <summary>Most mentions first.</summary>
    public required SuggestionModelResponse[] Models { get; init; }
}

/// <summary>A model version whose suggestions the caller accepted, and how many are still in their notes.</summary>
public record SuggestionModelResponse
{
    public required string Model { get; init; }
    public required string Version { get; init; }
    public required int Mentions { get; init; }
    public required int Notes { get; init; }
}

/// <summary>
/// The model versions behind the caller's accepted suggestions in this campaign (23c.6), read from
/// <see cref="SessionNote.SuggestedMentions"/> of the caller's own notes only: only the author can
/// revert them (invariant 4), so another member's notes are never counted.
/// </summary>
public class GetSuggestionModels(IDocumentSession session) : Endpoint<GetSuggestionModelsRequest, GetSuggestionModelsResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/suggestions/models");
    }

    public override async Task HandleAsync(GetSuggestionModelsRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);

        var notes = await SuggestionNotes.OfAuthor(session, req.CampaignId, member, ct);
        var models = notes
            .SelectMany(n => n.SuggestedMentions.Select(m => (n.Id, m.Model, m.Version)))
            .GroupBy(x => (x.Model, x.Version))
            .Select(g => new SuggestionModelResponse
            {
                Model = g.Key.Model,
                Version = g.Key.Version,
                Mentions = g.Count(),
                Notes = g.Select(x => x.Id).Distinct().Count(),
            })
            .OrderByDescending(m => m.Mentions)
            .ThenBy(m => m.Model, StringComparer.Ordinal)
            .ThenBy(m => m.Version, StringComparer.Ordinal)
            .ToArray();

        await SendAsync(new GetSuggestionModelsResponse { Models = models }, cancellation: ct);
    }
}

/// <summary>Reading the caller's notes that hold accepted suggestions.</summary>
public static class SuggestionNotes
{
    /// <summary>The notes <paramref name="author"/> wrote in the campaign with at least one <see cref="SessionNote.SuggestedMentions"/> row.</summary>
    public static async Task<IReadOnlyList<SessionNote>> OfAuthor(IQuerySession session, Guid campaignId, Member author, CancellationToken ct)
    {
        var authorId = author.MemberId;
        var notes = await session.Query<SessionNote>()
            .Where(n => n.CampaignId == campaignId && n.AuthorMemberId == authorId)
            .ToListAsync(ct);
        return notes.Where(n => n.SuggestedMentions is { Length: > 0 }).ToList();
    }
}
