namespace TakeInitiative.Dice;

/// <param name="Total">The result.</param>
/// <param name="Roll">The expression exactly as it was entered.</param>
/// <param name="Evaluation">What was rolled, e.g. <c>adv(1d20) [18, ~7] + 5 = 23</c>.</param>
public record DiceRoll(int Total, string Roll, string Evaluation);
