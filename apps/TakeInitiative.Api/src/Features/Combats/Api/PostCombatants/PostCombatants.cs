using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record PostCombatantsRequest
{
    public Guid CampaignId { get; init; }
    public Guid CombatId { get; init; }
    public required CombatantRequest[] Combatants { get; init; }
}

/// <summary>
/// One pick: an entry, or a plain <see cref="Name"/>, and <see cref="Count"/> copies of it.
/// With an entry, what is left out comes from its Stats (<see cref="CombatantDefaults"/>).
/// </summary>
public record CombatantRequest
{
    public Guid? EntryId { get; init; }
    /// <summary>Required without an entry. With one, it replaces the entry's name.</summary>
    public string? Name { get; init; }
    /// <summary>1 to 20; 1 when left out.</summary>
    public int? Count { get; init; }
    /// <summary>A dice expression. <c>1d20</c> when neither this nor the entry's Stats give one.</summary>
    public string? InitiativeRoll { get; init; }
    /// <summary>A dice expression, rolled once per combatant.</summary>
    public string? MaxHp { get; init; }
    public int? Ac { get; init; }
    public bool? Hidden { get; init; }
    public PlayersSee? PlayersSee { get; init; }
}

public class PostCombatantsRequestValidator : Validator<PostCombatantsRequest>
{
    public const int MaxCount = 20;

    public PostCombatantsRequestValidator()
    {
        RuleFor(x => x.Combatants)
            .NotEmpty().WithMessage("Add at least one combatant.")
            .Must(list => list is null || list.Sum(c => c.Count ?? 1) <= Combat.MaxCombatants)
            .WithMessage($"A combat can have at most {Combat.MaxCombatants} combatants.");
        RuleForEach(x => x.Combatants).ChildRules(c =>
        {
            c.RuleFor(x => x.Name)
                .Must((pick, name) => pick.EntryId is not null || !string.IsNullOrWhiteSpace(name))
                .WithMessage("A combatant needs an entry or a name.")
                .Must(name => name is null || name.Trim().Length <= Combatant.NameMaxLength)
                .WithMessage($"A combatant's name can be at most {Combatant.NameMaxLength} characters.");
            c.RuleFor(x => x.Count).InclusiveBetween(1, MaxCount).WithMessage($"Count must be between 1 and {MaxCount}.");
            c.RuleFor(x => x.InitiativeRoll).DiceExpression(() => Resolve<IDiceRoller>());
            c.RuleFor(x => x.MaxHp).DiceExpression(() => Resolve<IDiceRoller>());
            c.RuleFor(x => x.Ac).InclusiveBetween(0, Stats.AcMax).WithMessage($"AC must be between 0 and {Stats.AcMax}.");
            c.RuleFor(x => x.PlayersSee).IsInEnum();
        });
    }
}

/// <summary>
/// Adds waiting combatants with one <see cref="CombatantsAdded"/> (18a.3, 18a.4).
/// <list type="bullet">
/// <item>DMs add any, in a Draft or Active combat: entries they can see, or plain names.</item>
/// <item>A player adds one combatant a call: a Character entry they have claimed, with nothing
/// else set, and not one already in the combat. A player only sees a started combat, so this
/// is an Active one.</item>
/// </list>
/// </summary>
public class PostCombatants(IDocumentSession session, IHubContext<CampaignHub> hub, IDiceRoller dice)
    : Endpoint<PostCombatantsRequest, CombatResponse>
{
    public const string CombatantsErrorKey = "combatants";

    public override void Configure()
    {
        Post("/api/campaigns/{CampaignId}/combats/{CombatId}/combatants");
    }

    public override async Task HandleAsync(PostCombatantsRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        // A DM's entries do not depend on the combat, so they are loaded once, not per attempt.
        var dmPicks = member.Role == Role.DM ? await DmPicks(req, member, ct) : null;

        await this.Write(session, hub, campaign, req.CombatId, member, async combat =>
        {
            var picks = dmPicks ?? [await PlayerPick(req, combat, member, ct)];

            if (combat.Combatants.Count + picks.Sum(p => p.Count) > Combat.MaxCombatants)
            {
                ThrowError(new ValidationFailure(CombatantsErrorKey, $"A combat can have at most {Combat.MaxCombatants} combatants."));
            }

            var added = CombatantDefaults.Build(picks, member, combat.Combatants, dice, Random.Shared);
            if (added.IsFailure)
            {
                // The validator checked every expression, so a roll cannot fail here; if one does,
                // it is the request's fault, not the server's.
                ThrowError(new ValidationFailure(CombatantsErrorKey, added.Error));
            }

            return [new CombatantsAdded(Actor.Member(member.MemberId), [.. added.Value])];
        }, ct);
    }

    private async Task<IReadOnlyList<CombatantPick>> DmPicks(PostCombatantsRequest req, Member member, CancellationToken ct)
    {
        var picks = new List<CombatantPick>();
        foreach (var c in req.Combatants)
        {
            var entry = c.EntryId is { } entryId
                ? await this.RequireVisibleEntry(session, req.CampaignId, entryId, member, ct)
                : null;
            picks.Add(new CombatantPick
            {
                Entry = entry,
                Name = string.IsNullOrWhiteSpace(c.Name) ? null : c.Name,
                Count = c.Count ?? 1,
                InitiativeRoll = c.InitiativeRoll,
                MaxHp = c.MaxHp,
                Ac = c.Ac,
                Hidden = c.Hidden,
                PlayersSee = c.PlayersSee,
            });
        }
        return picks;
    }

    private async Task<CombatantPick> PlayerPick(PostCombatantsRequest req, Combat combat, Member member, CancellationToken ct)
    {
        var only = req.Combatants.Length == 1 ? req.Combatants[0] : null;
        if (only is null
            || only.EntryId is null
            || (only.Count ?? 1) != 1
            || only.Name is not null
            || only.InitiativeRoll is not null
            || only.MaxHp is not null
            || only.Ac is not null
            || only.Hidden is not null
            || only.PlayersSee is not null)
        {
            ThrowError("Players add one combatant at a time: a character they have claimed.", StatusCodes.Status403Forbidden);
        }

        var entry = await this.RequireVisibleEntry(session, req.CampaignId, only.EntryId.Value, member, ct);
        if (entry.Kind != EntryKind.Character || entry.ClaimedByMemberId != member.MemberId)
        {
            ThrowError("Players can only add a character they have claimed.", StatusCodes.Status403Forbidden);
        }
        var ids = entry.MentionIds();
        if (combat.Combatants.Any(c => c.EntryId is { } id && ids.Contains(id)))
        {
            ThrowError("Your character is already in this combat.", StatusCodes.Status409Conflict);
        }
        return new CombatantPick { Entry = entry, Count = 1 };
    }
}
