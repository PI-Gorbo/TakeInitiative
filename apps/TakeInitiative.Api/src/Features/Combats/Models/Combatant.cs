using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// One row of a combat (glossary: Combatant). Never sent as is: reads and pushes go through
/// <see cref="CombatView"/>, which redacts it per viewer and never carries <see cref="Tiebreak"/>.
/// </summary>
public record Combatant
{
    public Guid Id { get; init; }
    /// <summary>A copy of the entry's name when added (Goblin 3). Renaming the entry does not rename it.</summary>
    public string Name { get; init; } = "";
    /// <summary>The entry it came from, as added. A merged entry's id is kept and resolved on read (15g).</summary>
    public Guid? EntryId { get; init; }
    /// <summary>The member whose character this is: the entry's claimer when it was added.</summary>
    public Guid? OwnerMemberId { get; init; }
    /// <summary>A dice expression the next roll uses while the combatant waits. <c>1d20</c> by default.</summary>
    public string InitiativeRoll { get; init; } = DefaultInitiativeRoll;
    /// <summary>Null while waiting (glossary: Waiting); the next roll places it.</summary>
    public int? Initiative { get; init; }
    /// <summary>
    /// Breaks equal initiatives, drawn once when added, so the order is never ambiguous and
    /// adding a combatant never moves the placed ones. The server does every sort, so no
    /// response carries it.
    /// </summary>
    public int Tiebreak { get; init; }
    public int? Hp { get; init; }
    public int? MaxHp { get; init; }
    public int? Ac { get; init; }
    /// <summary>Hidden from players: not its row, its name or a count.</summary>
    public bool Hidden { get; init; }
    public PlayersSee PlayersSee { get; init; }
    public IReadOnlyList<Condition> Conditions { get; init; } = [];

    [JsonIgnore]
    public bool IsWaiting => Initiative is null;

    public const string DefaultInitiativeRoll = "1d20";
    public const int NameMaxLength = 100;
    public const int HpMin = -999;
    public const int HpMax = 9_999;
    public const int MaxHpMin = 1;
    public const int InitiativeMin = -99;
    public const int InitiativeMax = 99;

    /// <summary>The state a <c>PUT</c> replaces.</summary>
    [JsonIgnore]
    public CombatantState State => new(Name, Initiative, Hp, MaxHp, Ac, Hidden, PlayersSee, Conditions);

    public Combatant With(CombatantState state) => this with
    {
        Name = state.Name,
        Initiative = state.Initiative,
        Hp = state.Hp,
        MaxHp = state.MaxHp,
        Ac = state.Ac,
        Hidden = state.Hidden,
        PlayersSee = state.PlayersSee,
        Conditions = state.Conditions,
    };
}

/// <summary>
/// A combatant's whole editable state after an edit, so replaying <see cref="CombatantEdited"/>
/// needs no merge rules.
/// </summary>
public sealed record CombatantState(
    string Name,
    int? Initiative,
    int? Hp,
    int? MaxHp,
    int? Ac,
    bool Hidden,
    PlayersSee PlayersSee,
    IReadOnlyList<Condition> Conditions)
{
    // Records compare lists by reference; an edit compares by value.
    public bool SameAs(CombatantState other)
        => Name == other.Name
            && Initiative == other.Initiative
            && Hp == other.Hp
            && MaxHp == other.MaxHp
            && Ac == other.Ac
            && Hidden == other.Hidden
            && PlayersSee == other.PlayersSee
            && Conditions.SequenceEqual(other.Conditions);
}
