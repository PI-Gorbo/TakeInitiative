using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

public record PutEntryStatsRequest
{
    public Guid CampaignId { get; init; }
    public Guid EntryId { get; init; }
    /// <summary>A dice expression, such as <c>1d20+2</c>. Blank or null for none.</summary>
    public string? InitiativeRoll { get; init; }
    /// <summary>A dice expression, such as <c>2d8+2</c> or <c>11</c>. Step 18 rolls it when a combatant is added.</summary>
    public string? MaxHp { get; init; }
    /// <summary>0 to 99, or null for none.</summary>
    public int? Ac { get; init; }
}

/// <summary>
/// The dice expressions are checked with <see cref="IDiceRoller.Check"/>, the combat's own
/// checker, so whatever is stored rolls in step 18. The message is the dice language's.
/// </summary>
public class PutEntryStatsRequestValidator : Validator<PutEntryStatsRequest>
{
    public PutEntryStatsRequestValidator()
    {
        RuleFor(x => x.InitiativeRoll).DiceExpression(this);
        RuleFor(x => x.MaxHp).DiceExpression(this);
        RuleFor(x => x.Ac).InclusiveBetween(0, Stats.AcMax).WithMessage($"AC must be between 0 and {Stats.AcMax}.");
    }
}

public static class DiceExpressionRules
{
    public static IRuleBuilderOptionsConditions<T, string?> DiceExpression<T>(this IRuleBuilder<T, string?> rule, Validator<T> validator)
        where T : class
        => rule.Custom((value, context) =>
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            var trimmed = value.Trim();
            if (trimmed.Length > Stats.ExpressionMaxLength)
            {
                context.AddFailure($"A dice expression can be at most {Stats.ExpressionMaxLength} characters long.");
                return;
            }
            var check = validator.Resolve<IDiceRoller>().Check(trimmed);
            if (check.IsFailure)
            {
                context.AddFailure(check.Error);
            }
        });
}

/// <summary>
/// Sets a Character entry's stats (glossary: Stats, 15g.3). All null (or blank) clears them.
/// <list type="bullet">
/// <item>Only a <c>Character</c> has stats (409 <c>errors.kind</c>).</item>
/// <item>On a claimed entry, the claimer and the DMs write them; on an unclaimed one, the DMs
/// only (<see cref="EntryStats.CanWrite"/>, 403 otherwise).</item>
/// </list>
/// The same stats append nothing. The response is the caller's view, and
/// <c>entryStatsChanged</c> goes only to the members who may read them (so an NPC's stats ping
/// the DMs only). Stats leave <c>updatedAt</c> alone, so nothing on the summary moves.
/// </summary>
public class PutEntryStats(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PutEntryStatsRequest, EntryResponse>
{
    public const string KindErrorKey = "kind";

    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/entries/{EntryId}/stats");
    }

    public override async Task HandleAsync(PutEntryStatsRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var entry = await this.RequireVisibleEntry(session, req.CampaignId, req.EntryId, member, ct);

        if (entry.Kind != EntryKind.Character)
        {
            ThrowError(new ValidationFailure(KindErrorKey, "Only a Character has stats."), StatusCodes.Status409Conflict);
        }
        if (!EntryStats.CanWrite(entry, member))
        {
            ThrowError(entry.ClaimedByMemberId is null
                ? "Only the DMs can set the stats of a character nobody has claimed."
                : "Only the claimer and the DMs can set a player character's stats.", StatusCodes.Status403Forbidden);
        }

        var stats = Stats.Of(req.InitiativeRoll, req.MaxHp, req.Ac);
        if (stats != entry.Stats)
        {
            session.Events.Append(entry.Id, new EntryStatsChanged(Actor.Member(member.MemberId), stats));
            await session.SaveChangesAsync(ct);
            var before = entry;
            entry = (await session.LoadAsync<Entry>(entry.Id, ct))!;
            await hub.NotifyEntryStatsChanged(campaign.Members, before, entry);
        }

        await SendAsync(EntryResponse.From(entry, member), cancellation: ct);
    }
}
