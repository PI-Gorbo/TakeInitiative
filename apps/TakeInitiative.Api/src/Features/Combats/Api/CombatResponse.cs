using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// One combat as one viewer may see it (<see cref="CombatView.For"/>), for reads, every write's
/// response and the <c>combatChanged</c> push. Combatants come in the initiative order, then
/// the waiting ones in the order they were added.
/// </summary>
public record CombatResponse
{
    public required Guid Id { get; init; }
    public required Guid CampaignId { get; init; }
    public required Guid SessionId { get; init; }
    public required string Name { get; init; }
    public required CombatStatus Status { get; init; }
    public required int Round { get; init; }
    /// <summary>Whose turn it is. Null with no turn, and for a player when it is a hidden combatant's.</summary>
    public Guid? TurnCombatantId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public required CombatantResponse[] Combatants { get; init; }
}

/// <summary>
/// One combatant as one viewer may see it. The redacted fields are left out of the JSON
/// altogether rather than sent as null, so a player's payload has no <c>hp</c>, <c>maxHp</c>,
/// <c>ac</c> or <c>band</c> key it may not read. There is no <c>tiebreak</c>: the server does
/// every sort.
/// </summary>
public record CombatantResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    /// <summary>The entry, resolved through merges, when the viewer can see it. Otherwise the name is plain text.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? EntryId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? OwnerMemberId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Initiative { get; init; }
    /// <summary>No initiative yet (glossary: Waiting): the next roll places it.</summary>
    public required bool Waiting { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Hp { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxHp { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HpBand? Band { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Ac { get; init; }
    /// <summary>For a player, always false except on their own combatant.</summary>
    public required bool Hidden { get; init; }
    /// <summary>For a player, the level this row was redacted to (their own: its real value).</summary>
    public required PlayersSee PlayersSee { get; init; }
    public required Condition[] Conditions { get; init; }
    /// <summary>The dice expression the next roll uses. DMs, and a player on their own combatant.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InitiativeRoll { get; init; }
}

/// <summary>A combat in the Combat tab's list. <see cref="CombatantCount"/> leaves out hidden combatants for players.</summary>
public record CombatSummaryResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required CombatStatus Status { get; init; }
    public required int Round { get; init; }
    public required Guid SessionId { get; init; }
    public required int SessionNumber { get; init; }
    public required int CombatantCount { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
}

/// <summary><c>combatChanged</c>: the receiver's own view of the combat, and its summary for the list.</summary>
public record CombatChangedMessage(CombatResponse Combat, CombatSummaryResponse Summary);
