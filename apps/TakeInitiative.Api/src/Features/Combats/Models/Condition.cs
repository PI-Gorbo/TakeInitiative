namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// A condition on a combatant (glossary: Condition): a text label with an optional note.
/// Visible to players on every combatant they can see; it is what the table sees happen.
/// </summary>
public sealed record Condition(string Label, string? Note)
{
    public const int LabelMaxLength = 40;
    public const int NoteMaxLength = 200;
    public const int MaxPerCombatant = 20;

    /// <summary>Trims both; a blank note is null.</summary>
    public static Condition Of(string label, string? note)
        => new(label.Trim(), string.IsNullOrWhiteSpace(note) ? null : note.Trim());
}
