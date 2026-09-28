using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Combats;

public record PutCombatantRequest
{
    public Guid CampaignId { get; init; }
    public Guid CombatId { get; init; }
    public Guid CombatantId { get; init; }
    public required string Name { get; init; }
    /// <summary>Null makes the combatant wait for the next roll.</summary>
    public int? Initiative { get; init; }
    public int? Hp { get; init; }
    public int? MaxHp { get; init; }
    public int? Ac { get; init; }
    public required bool Hidden { get; init; }
    public required PlayersSee PlayersSee { get; init; }
    public required ConditionRequest[] Conditions { get; init; }
}

public record ConditionRequest
{
    public required string Label { get; init; }
    public string? Note { get; init; }
}

public class PutCombatantRequestValidator : Validator<PutCombatantRequest>
{
    public PutCombatantRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("A combatant needs a name.")
            .Must(name => (name ?? "").Trim().Length <= Combatant.NameMaxLength)
            .WithMessage($"A combatant's name can be at most {Combatant.NameMaxLength} characters.");
        RuleFor(x => x.Initiative).InclusiveBetween(Combatant.InitiativeMin, Combatant.InitiativeMax)
            .WithMessage($"Initiative must be between {Combatant.InitiativeMin} and {Combatant.InitiativeMax}.");
        RuleFor(x => x.Hp).InclusiveBetween(Combatant.HpMin, Combatant.HpMax)
            .WithMessage($"HP must be between {Combatant.HpMin} and {Combatant.HpMax}.");
        RuleFor(x => x.MaxHp).InclusiveBetween(Combatant.MaxHpMin, Combatant.HpMax)
            .WithMessage($"Max HP must be between {Combatant.MaxHpMin} and {Combatant.HpMax}.");
        RuleFor(x => x.Ac).InclusiveBetween(0, Stats.AcMax).WithMessage($"AC must be between 0 and {Stats.AcMax}.");
        RuleFor(x => x.PlayersSee).IsInEnum();
        RuleFor(x => x.Conditions)
            .NotNull()
            .Must(list => list is null || list.Length <= Condition.MaxPerCombatant)
            .WithMessage($"A combatant can have at most {Condition.MaxPerCombatant} conditions.");
        RuleForEach(x => x.Conditions).ChildRules(c =>
        {
            c.RuleFor(x => x.Label)
                .Must(label => !string.IsNullOrWhiteSpace(label)).WithMessage("A condition needs a label.")
                .Must(label => (label ?? "").Trim().Length <= Condition.LabelMaxLength)
                .WithMessage($"A condition's label can be at most {Condition.LabelMaxLength} characters.");
            c.RuleFor(x => x.Note)
                .Must(note => note is null || note.Trim().Length <= Condition.NoteMaxLength)
                .WithMessage($"A condition's note can be at most {Condition.NoteMaxLength} characters.");
        });
    }
}

/// <summary>
/// Replaces a combatant's editable state with one <see cref="CombatantEdited"/>; the same state
/// appends nothing. DMs edit every field. A player edits only their own combatant, and only
/// its HP, max HP, conditions and, while it waits, its initiative: sending any other field
/// changed is a 403. Edits are last-write-wins per combatant (18's Notes).
/// </summary>
public class PutCombatant(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PutCombatantRequest, CombatResponse>
{
    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/combats/{CombatId}/combatants/{CombatantId}");
    }

    public override async Task HandleAsync(PutCombatantRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (campaign, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var (stream, combat) = await this.Open(session, req.CampaignId, req.CombatId, member, ct);
        var combatant = this.RequireChangeableCombatant(combat, req.CombatantId, member);

        var state = new CombatantState(
            Name: req.Name.Trim(),
            Initiative: req.Initiative,
            Hp: req.Hp,
            MaxHp: req.MaxHp,
            Ac: req.Ac,
            Hidden: req.Hidden,
            PlayersSee: req.PlayersSee,
            Conditions: req.Conditions.Select(c => Condition.Of(c.Label, c.Note)).ToList());

        var forbidden = CombatAccess.ForbiddenChanges(combatant.State, state, member);
        if (forbidden.Count > 0)
        {
            ThrowError($"Players can't change these on their combatant: {string.Join(", ", forbidden)}.", StatusCodes.Status403Forbidden);
        }

        if (!state.SameAs(combatant.State))
        {
            stream.AppendOne(new CombatantEdited(Actor.Member(member.MemberId), combatant.Id, state));
        }
        await this.Commit(session, hub, campaign, combat.Id, member, ct);
    }
}
