namespace TakeInitiative.Dice.Tests;

/// <summary>Spec §4: each limit just under and just over the boundary.</summary>
public class LimitTests
{
    [Theory]
    [InlineData("1000d6", null)]
    [InlineData("1001d6", DiceErrorCodes.PoolTooLarge)]
    [InlineData("1d1000", null)]
    [InlineData("1d1001", DiceErrorCodes.TooManySides)]
    [InlineData("500d6 + 500d6", null)]
    [InlineData("500d6 + 501d6", DiceErrorCodes.TooManyDice)]
    [InlineData("998d6 + adv(1d20)", null)]
    [InlineData("999d6 + adv(1d20)", DiceErrorCodes.TooManyDice)]
    [InlineData("kh1(1000d6)", null)]
    [InlineData("10000", null)]
    [InlineData("10001", DiceErrorCodes.NumberTooLarge)]
    [InlineData("99999999999999999999999999", DiceErrorCodes.NumberTooLarge)]
    public void Boundaries(string expression, string? code)
    {
        var result = DiceLanguage.Check(expression);

        if (code is null)
        {
            Assert.True(result.IsSuccess, result.IsFailure ? ParserTests.Describe(result.Error) : "");
        }
        else
        {
            Assert.Equal(code, Assert.Single(result.Error).Code);
        }
    }

    [Fact]
    public void Absurd_pool_reports_both_count_and_sides()
    {
        var errors = DiceLanguage.Check("99999d99999").Error;

        Assert.Equal([DiceErrorCodes.PoolTooLarge, DiceErrorCodes.TooManySides], errors.Select(e => e.Code));
    }

    [Fact]
    public void Expression_length()
    {
        var atLimit = "1" + new string(' ', DiceLimits.MaxExpressionLength - 1);
        var overLimit = atLimit + " ";

        Assert.True(DiceLanguage.Check(atLimit).IsSuccess);
        Assert.Equal(DiceErrorCodes.ExpressionTooLong, Assert.Single(DiceLanguage.Check(overLimit).Error).Code);
    }

    [Fact]
    public void Nesting_depth()
    {
        string Nested(int depth) => new string('(', depth) + "1" + new string(')', depth);

        Assert.True(DiceLanguage.Check(Nested(DiceLimits.MaxNestingDepth)).IsSuccess);

        var error = Assert.Single(DiceLanguage.Check(Nested(DiceLimits.MaxNestingDepth + 1)).Error);
        Assert.Equal(DiceErrorCodes.ExpressionTooDeep, error.Code);
        Assert.Equal(DiceLimits.MaxNestingDepth, error.Position);
    }

    [Fact]
    public void Limits_are_enforced_before_rolling()
    {
        // ScriptedRandom with no rolls throws if anything is rolled.
        var result = DiceLanguage.Roll("1001d6", new ScriptedRandom());

        Assert.Equal(DiceErrorCodes.PoolTooLarge, Assert.Single(result.Error).Code);
    }
}
