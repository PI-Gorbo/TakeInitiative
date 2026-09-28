using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record PostCombatRequest
{
    public Guid CampaignId { get; init; }
    public required string Name { get; init; }
}

public class PostCombatRequestValidator : Validator<PostCombatRequest>
{
    public PostCombatRequestValidator()
    {
        RuleFor(x => x.Name).CombatName();
    }
}

public static class CombatNameRules
{
    /// <summary>1 to 100 characters once trimmed.</summary>
    public static IRuleBuilderOptions<T, string> CombatName<T>(this IRuleBuilder<T, string> rule)
        => rule
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("A combat needs a name.")
            .Must(name => (name ?? "").Trim().Length <= Combat.NameMaxLength)
            .WithMessage($"A combat's name can be at most {Combat.NameMaxLength} characters.");
}

/// <summary>
/// A DM starts a <c>Draft</c> combat in the current session. With no session yet it is a 409:
/// a combat is a card in a session. Players get 403. The Draft is pushed to the DMs only.
/// </summary>
public class PostCombat(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PostCombatRequest, CombatResponse>
{
    public override void Configure()
    {
        Post("/api/campaigns/{CampaignId}/combats");
    }

    public override async Task HandleAsync(PostCombatRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        this.RequireDm(member, "Only a DM can create a combat.");
        var current = await session.CurrentSession(req.CampaignId, ct);
        if (current is null)
        {
            ThrowError("Start Session 1 first.", StatusCodes.Status409Conflict);
        }

        var combatId = Guid.NewGuid();
        session.Events.StartStream<Combat>(combatId,
            new CombatCreated(Actor.Member(member.MemberId), req.CampaignId, current.Id, Combat.NormaliseName(req.Name)));
        await session.SaveChangesAsync(ct);

        var combat = (await session.LoadAsync<Combat>(combatId, ct))!;
        await hub.NotifyCombatChanged(session, campaign, combat, ct);
        await SendAsync(await CombatEntries.ViewFor(session, combat, member, ct), cancellation: ct);
    }
}
