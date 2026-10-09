using TakeInitiative.Dice.Typing;

namespace TakeInitiative.Dice.Tests;

public class TypeCheckerTests
{
    /// <summary>
    /// Every row of the spec §3 rejection table. Asserts the code and span, never the
    /// message text — messages get reworded; codes are the contract.
    /// </summary>
    [Theory]
    [InlineData("adv(2d6)", DiceErrorCodes.AdvRequiresD20, 4, 3)]
    [InlineData("adv(1d6)", DiceErrorCodes.AdvRequiresD20, 4, 3)]
    [InlineData("adv(2d20)", DiceErrorCodes.AdvRequiresD20, 4, 4)]
    [InlineData("dis(2d20)", DiceErrorCodes.AdvRequiresD20, 4, 4)]
    [InlineData("adv(kh1(2d20))", DiceErrorCodes.AdvRequiresD20, 4, 9)]
    [InlineData("adv(20)", DiceErrorCodes.AdvRequiresD20, 4, 2)]
    [InlineData("kh1(3 + 4)", DiceErrorCodes.KeepRequiresPool, 4, 5)]
    [InlineData("kh1(1d6 + 1d6)", DiceErrorCodes.KeepRequiresPool, 4, 9)]
    [InlineData("kh1(max(1d6, 2))", DiceErrorCodes.KeepRequiresPool, 4, 11)]
    [InlineData("kh3(2d10)", DiceErrorCodes.KeepExceedsPool, 0, 9)]
    [InlineData("kl3(2d10)", DiceErrorCodes.KeepExceedsPool, 0, 9)]
    [InlineData("kh2(kh1(3d6))", DiceErrorCodes.KeepExceedsPool, 0, 13)]
    [InlineData("kh0(2d10)", DiceErrorCodes.KeepAtLeastOne, 0, 9)]
    [InlineData("0d6", DiceErrorCodes.DiceCountZero, 0, 3)]
    [InlineData("1d0", DiceErrorCodes.DiceSidesZero, 0, 3)]
    [InlineData("1d6 / 0", DiceErrorCodes.DivideByZero, 6, 1)]
    [InlineData("2d20kh1", DiceErrorCodes.LegacyKeepSyntax, 0, 7)]
    [InlineData("2d20kl1", DiceErrorCodes.LegacyKeepSyntax, 0, 7)]
    [InlineData("1 + 2d20kh", DiceErrorCodes.LegacyKeepSyntax, 4, 6)]
    [InlineData("1 +  d20kh", DiceErrorCodes.LegacyKeepSyntax, 5, 5)]
    [InlineData("adv( d6 )", DiceErrorCodes.AdvRequiresD20, 5, 2)]
    [InlineData("1 + kh3( 2d10 ) + 2", DiceErrorCodes.KeepExceedsPool, 4, 11)]
    public void Rejections(string expression, string code, int position, int length)
    {
        var error = Assert.Single(DiceLanguage.Check(expression).Error);

        Assert.Equal(code, error.Code);
        Assert.Equal((position, length), (error.Position, error.Length));
    }

    [Fact]
    public void Legacy_syntax_error_says_how_to_rewrite_it()
    {
        var error = Assert.Single(DiceLanguage.Check("2d20kh1").Error);

        Assert.Contains("kh1(2d20)", error.Message);
    }

    [Fact]
    public void Adv_on_several_d20s_suggests_kh()
    {
        var error = Assert.Single(DiceLanguage.Check("adv(2d20)").Error);

        Assert.Contains("kh1(2d20)", error.Message);
    }

    [Fact]
    public void Every_error_is_reported_not_just_the_first()
    {
        var errors = DiceLanguage.Check("kh3(2d10) + adv(1d6) + 0d4").Error;

        Assert.Equal(
            [DiceErrorCodes.KeepExceedsPool, DiceErrorCodes.AdvRequiresD20, DiceErrorCodes.DiceCountZero],
            errors.Select(e => e.Code));
    }

    [Theory]
    [InlineData("3", "Scalar")]
    [InlineData("1d20", "Pool(1, 20)")]
    [InlineData("4d6", "Pool(4, 6)")]
    [InlineData("kh2(4d6)", "Pool(2, 6)")]
    [InlineData("kh1(kh2(3d20))", "Pool(1, 20)")]
    [InlineData("adv(1d20)", "Pool(1, 20)")]
    [InlineData("(4d6)", "Pool(4, 6)")]
    [InlineData("1d20 + 3", "Scalar")]
    [InlineData("2 * 1d6", "Scalar")]
    [InlineData("max(1d4, 2)", "Scalar")]
    public void Types(string expression, string type)
    {
        Assert.Equal(type, DiceLanguage.Check(expression).Value.Type.ToString());
    }

    [Fact]
    public void Keep_count_defaults_to_one()
    {
        var keep = Assert.IsType<TypedKeep>(DiceLanguage.Check("kh(2d20)").Value);

        Assert.Equal(1, keep.Keep);
    }

    [Fact]
    public void Keeping_every_die_is_allowed()
    {
        Assert.True(DiceLanguage.Check("kh2(2d10)").IsSuccess);
    }

    [Fact]
    public void Nested_keeps_can_select_from_an_advantage()
    {
        Assert.True(DiceLanguage.Check("kh1(adv(1d20))").IsSuccess);
    }

    [Fact]
    public void Division_by_a_rolled_value_is_not_a_check_error()
    {
        Assert.True(DiceLanguage.Check("10 / (1d2 - 1)").IsSuccess);
    }
}
