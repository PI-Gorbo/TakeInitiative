namespace TakeInitiative.Dice.Tests;

public class EvaluatorTests
{
    /// <summary>Every row of the spec §5 table, plus the other functions.</summary>
    [Theory]
    [InlineData("1d20 + 3", new[] { 14 }, 17, "1d20 [14] + 3 = 17")]
    [InlineData("2d6 + 1d4 + 2", new[] { 3, 5, 2 }, 12, "2d6 [5, 3] + 1d4 [2] + 2 = 12")]
    [InlineData("adv(1d20) + 5", new[] { 7, 18 }, 23, "adv(1d20) [18, ~7] + 5 = 23")]
    [InlineData("kh2(4d6)", new[] { 3, 6, 1, 5 }, 11, "kh2(4d6) [6, 5, ~3, ~1] = 11")]
    [InlineData("kh1(kh2(3d20))", new[] { 12, 3, 18 }, 18, "kh1(kh2(3d20)) [18, ~12, ~3] = 18")]
    [InlineData("10 - 1d4", new[] { 3 }, 7, "10 - 1d4 [3] = 7")]
    [InlineData("2 * (1d6 + 1)", new[] { 4 }, 10, "2 * (1d6 [4] + 1) = 10")]
    [InlineData("max(1d4, 2)", new[] { 1 }, 2, "max(1d4 [1], 2) = 2")]
    [InlineData("dis(1d20)", new[] { 7, 18 }, 7, "dis(1d20) [7, ~18] = 7")]
    [InlineData("kl1(2d20)", new[] { 9, 4 }, 4, "kl1(2d20) [4, ~9] = 4")]
    [InlineData("kl2(kh3(4d6))", new[] { 6, 2, 4, 5 }, 9, "kl2(kh3(4d6)) [5, 4, ~6, ~2] = 9")]
    [InlineData("kh1(adv(1d20))", new[] { 3, 11 }, 11, "kh1(adv(1d20)) [11, ~3] = 11")]
    [InlineData("min(1d20 + 5, 20)", new[] { 19 }, 20, "min(1d20 [19] + 5, 20) = 20")]
    [InlineData("max(1d6, 1d6, 1d6)", new[] { 2, 6, 3 }, 6, "max(1d6 [2], 1d6 [6], 1d6 [3]) = 6")]
    [InlineData("d20", new[] { 8 }, 8, "1d20 [8] = 8")]
    [InlineData("1D20+3", new[] { 14 }, 17, "1d20 [14] + 3 = 17")]
    [InlineData("7", new int[0], 7, "7 = 7")]
    [InlineData("1d20 / 2", new[] { 7 }, 3, "1d20 [7] / 2 = 3")]
    [InlineData("(1 - 1d20) / 4", new[] { 7 }, -2, "(1 - 1d20 [7]) / 4 = -2")]
    [InlineData("10 - 1d4 - 1d4", new[] { 1, 2 }, 7, "10 - 1d4 [1] - 1d4 [2] = 7")]
    public void Evaluations(string expression, int[] rolls, int total, string evaluation)
    {
        var random = new ScriptedRandom(rolls);

        var roll = DiceLanguage.Roll(expression, random);

        Assert.True(roll.IsSuccess, roll.IsFailure ? ParserTests.Describe(roll.Error) : "");
        Assert.Equal(total, roll.Value.Total);
        Assert.Equal(evaluation, roll.Value.Evaluation);
        Assert.Equal(expression, roll.Value.Roll);
        Assert.Equal(0, random.Remaining);
    }

    [Fact]
    public void Rolled_zero_divisor_fails_at_evaluation()
    {
        var roll = DiceLanguage.Roll("10 / (1d2 - 1)", new ScriptedRandom(1));

        var error = Assert.Single(roll.Error);
        Assert.Equal(DiceErrorCodes.DivideByZero, error.Code);
        Assert.Equal((6, 7), (error.Position, error.Length));
    }

    [Fact]
    public void Overflow_fails_rather_than_wrapping()
    {
        var roll = DiceLanguage.Roll("10000 * 10000 * 10000", new ScriptedRandom());

        Assert.Equal(DiceErrorCodes.ResultOutOfRange, Assert.Single(roll.Error).Code);
    }

    [Fact]
    public void Real_rolls_stay_in_range()
    {
        var roll = DiceLanguage.Roll("1000d6", new Random(1234));

        Assert.InRange(roll.Value.Total, 1000, 6000);
    }

    [Fact]
    public void RollD20_builds_a_single_die()
    {
        var roll = DiceLanguage.RollD20(new ScriptedRandom(13));

        Assert.Equal(new DiceRoll(13, "1d20", "1d20 [13] = 13"), roll);
    }
}
