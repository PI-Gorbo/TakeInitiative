using FastEndpoints;
using FluentValidation;
using Marten;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record GetCombatsRequest
{
    public Guid CampaignId { get; init; }
    /// <summary>A comma list of <c>Draft</c>, <c>Active</c> and <c>Finished</c>. Omit it for every status.</summary>
    public string? Status { get; init; }
}

public class GetCombatsRequestValidator : Validator<GetCombatsRequest>
{
    public GetCombatsRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => GetCombats.ParseStatuses(status) is not null)
            .WithMessage("status is a comma list of Draft, Active and Finished.");
    }
}

public record GetCombatsResponse
{
    /// <summary>The combats the caller can see, newest first.</summary>
    public required CombatSummaryResponse[] Combats { get; init; }
}

/// <summary>
/// The Combat tab's list: summaries of the combats the caller can see, newest first. Players
/// see only combats that have started, and their counts leave hidden combatants out.
/// </summary>
public class GetCombats(IDocumentSession session) : Endpoint<GetCombatsRequest, GetCombatsResponse>
{
    public override void Configure()
    {
        Get("/api/campaigns/{CampaignId}/combats");
    }

    /// <summary>The statuses asked for, every one when blank, or null when one is unknown.</summary>
    public static CombatStatus[]? ParseStatuses(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return Enum.GetValues<CombatStatus>();
        }
        var parsed = new List<CombatStatus>();
        foreach (var part in status.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Enum.TryParse<CombatStatus>(part, ignoreCase: true, out var value) || !Enum.IsDefined(value))
            {
                return null;
            }
            parsed.Add(value);
        }
        return parsed.Distinct().ToArray();
    }

    public override async Task HandleAsync(GetCombatsRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var statuses = ParseStatuses(req.Status)!;

        var query = session.Query<Combat>().Where(c => c.CampaignId == req.CampaignId);
        if (member.Role != Role.DM)
        {
            query = query.Where(c => c.StartedAt != null);
        }
        var combats = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);

        var sessionIds = combats.Select(c => c.SessionId).Distinct().ToArray();
        var numbers = sessionIds.Length == 0
            ? new Dictionary<Guid, int>()
            : (await session.LoadManyAsync<Session>(ct, sessionIds)).ToDictionary(s => s.Id, s => s.Number);

        await SendAsync(new GetCombatsResponse
        {
            Combats = combats
                // Status is filtered here: a campaign has tens of combats, not thousands.
                .Where(c => statuses.Contains(c.Status) && CombatView.CanSee(c, member))
                .Select(c => CombatView.Summary(c, member, numbers.GetValueOrDefault(c.SessionId)))
                .ToArray(),
        }, cancellation: ct);
    }
}
