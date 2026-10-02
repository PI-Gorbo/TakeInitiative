using JasperFx.Events;

namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// Inline projection of a Combat stream (stream id = combat id), design §8. Never sent as
/// is: every read and push goes through <see cref="CombatView"/> (invariant 8).
/// <para>
/// The turn is <see cref="TurnCombatantId"/>, not an index into the order, so slotting in a
/// late joiner, removing a combatant or reordering never has to fix it up (18's Notes).
/// </para>
/// </summary>
public record Combat
{
    public Guid Id { get; init; }
    public Guid CampaignId { get; init; }
    /// <summary>Where the combat is a card. The first roll moves a Draft to the current session (18b).</summary>
    public Guid SessionId { get; init; }
    public string Name { get; init; } = "";
    public CombatStatus Status { get; init; }
    /// <summary>0 while Draft; 1 from the first roll.</summary>
    public int Round { get; init; }
    public Guid? TurnCombatantId { get; init; }
    public IReadOnlyList<Combatant> Combatants { get; init; } = [];
    /// <summary>
    /// The entries of the combatants, as added, for 18e's "an entry's combats" (a GIN index,
    /// like <c>Entry.ArticleMentionIds</c>). Redaction still applies when it is read.
    /// </summary>
    public Guid[] EntryIds { get; init; } = [];
    public Guid CreatedByMemberId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    /// <summary>Set by the first roll. Players see a combat only from then (18a.4).</summary>
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }

    public const int NameMaxLength = 100;
    public const int MaxCombatants = 50;

    public Combatant? Find(Guid combatantId) => Combatants.FirstOrDefault(c => c.Id == combatantId);

    public static Combat Create(IEvent<CombatCreated> @event) => new()
    {
        Id = @event.StreamId,
        CampaignId = @event.Data.CampaignId,
        SessionId = @event.Data.SessionId,
        Name = @event.Data.Name,
        Status = CombatStatus.Draft,
        CreatedByMemberId = @event.Data.Actor.MemberId,
        CreatedAt = @event.Timestamp,
    };

    public Combat Apply(CombatantsAdded e) => WithCombatants([.. Combatants, .. e.Combatants]);

    public Combat Apply(CombatantEdited e)
    {
        var before = Find(e.CombatantId);
        if (before is null)
        {
            return this;
        }
        // Clearing the initiative of the combatant whose turn it is makes it wait, so the turn
        // passes on as if it had ended.
        var passed = TurnCombatantId == e.CombatantId && e.State.Initiative is null ? PassTurnFrom(e.CombatantId) : this;
        return passed.WithCombatants(Combatants
                .Select(c => c.Id == e.CombatantId ? c.With(e.State) with { Tiebreak = e.Tiebreak ?? c.Tiebreak } : c)
                .ToList())
            .WithTurnOnTop();
    }

    public Combat Apply(CombatantRemoved e)
    {
        var passed = TurnCombatantId == e.CombatantId ? PassTurnFrom(e.CombatantId) : this;
        return passed.WithCombatants(Combatants.Where(c => c.Id != e.CombatantId).ToList());
    }

    /// <summary>
    /// 18b's roll, projected here so a started combat can be read and redacted. The first roll
    /// starts a Draft; later rolls slot in the late joiners and leave the turn where it is.
    /// </summary>
    public Combat Apply(IEvent<InitiativeRolled> @event)
    {
        var rolls = @event.Data.Rolls.ToDictionary(r => r.CombatantId, r => r.Total);
        var rolled = WithCombatants(Combatants
            .Select(c => rolls.TryGetValue(c.Id, out var total) ? c with { Initiative = total } : c)
            .ToList());
        if (Status == CombatStatus.Draft)
        {
            rolled = rolled with
            {
                Status = CombatStatus.Active,
                Round = 1,
                StartedAt = @event.Timestamp,
                SessionId = @event.Data.SessionId ?? SessionId,
            };
        }
        return rolled.WithTurnOnTop();
    }

    /// <summary>End turn (18b.2): the event says where the turn went and in which round.</summary>
    public Combat Apply(TurnEnded e) => this with { TurnCombatantId = e.ToCombatantId, Round = e.Round };

    /// <summary>Finish (18b.5): read-only from here, with no turn. A Draft finished this way was discarded.</summary>
    public Combat Apply(IEvent<CombatFinished> @event) => this with
    {
        Status = CombatStatus.Finished,
        FinishedAt = @event.Timestamp,
        TurnCombatantId = null,
    };

    /// <summary>The turn moves to the combatant after <paramref name="combatantId"/>, counting a round on the wrap.</summary>
    private Combat PassTurnFrom(Guid combatantId)
    {
        var (next, wrapped) = CombatOrder.After(CombatOrder.Ordered(this), combatantId);
        return this with { TurnCombatantId = next, Round = wrapped ? Round + 1 : Round };
    }

    /// <summary>An active combat with no turn gives it to the top of the order, once there is one.</summary>
    private Combat WithTurnOnTop()
        => Status == CombatStatus.Active && TurnCombatantId is null
            ? this with { TurnCombatantId = CombatOrder.Ordered(this).FirstOrDefault()?.Id }
            : this;

    private Combat WithCombatants(IReadOnlyList<Combatant> combatants) => this with
    {
        Combatants = combatants,
        EntryIds = combatants.Where(c => c.EntryId is not null).Select(c => c.EntryId!.Value).Distinct().ToArray(),
    };

    /// <summary>A name is trimmed.</summary>
    public static string NormaliseName(string name) => name.Trim();
}
