using TakeInitiative.Dice.Evaluation;

namespace TakeInitiative.Dice.Tests;

public class ParserTests
{
    /// <summary>Every row of the spec §2 examples table.</summary>
    [Theory]
    [InlineData("1d20")]
    [InlineData("d20")]
    [InlineData("1d20 + 2d4 + 3")]
    [InlineData("adv(1d20)")]
    [InlineData("adv(d20)")]
    [InlineData("dis(1d20)")]
    [InlineData("kh1(2d10)")]
    [InlineData("kh(2d10)")]
    [InlineData("kh2(4d6)")]
    [InlineData("kl1(2d20)")]
    [InlineData("kh1(kh2(3d20))")]
    [InlineData("adv(1d20) + 5")]
    [InlineData("10 - 1d4")]
    [InlineData("2 * 1d6 + 1")]
    [InlineData("2 * (1d6 + 1)")]
    [InlineData("1d20 / 2")]
    [InlineData("max(1d4, 2)")]
    [InlineData("min(1d20 + 5, 20)")]
    [InlineData("max(1d6, 1d6, 1d6)")]
    public void Spec_examples_are_valid(string expression)
    {
        var result = DiceLanguage.Check(expression);

        Assert.True(result.IsSuccess, result.IsFailure ? Describe(result.Error) : "");
    }

    [Theory]
    [InlineData("d20", "1d20")]
    [InlineData("D20", "1d20")]
    [InlineData(" 1 d 20 ", "1d20")]
    [InlineData("1D20+3", "1d20 + 3")]
    [InlineData("KH(2D20)", "kh1(2d20)")]
    [InlineData("ADV(d20)", "adv(1d20)")]
    [InlineData("Dis( 1d20 )", "dis(1d20)")]
    [InlineData("MAX(1d4,2)", "max(1d4, 2)")]
    [InlineData("(1d20)", "1d20")]
    [InlineData("((3))", "3")]
    public void Whitespace_casing_and_defaults_normalise(string expression, string normalised)
    {
        Assert.Equal(normalised, Normalise(expression));
    }

    [Theory]
    [InlineData("2 * 1d6 + 1", "2 * 1d6 + 1")]
    [InlineData("1 + 2 * 3", "1 + 2 * 3")]
    [InlineData("(1 + 2) * 3", "(1 + 2) * 3")]
    [InlineData("2*(1d6+1)", "2 * (1d6 + 1)")]
    [InlineData("10 - 2 - 3", "10 - 2 - 3")]
    [InlineData("(10 - 2) - 3", "10 - 2 - 3")]
    [InlineData("10 - (2 - 3)", "10 - (2 - 3)")]
    [InlineData("8 / 4 / 2", "8 / 4 / 2")]
    [InlineData("8 / (4 / 2)", "8 / (4 / 2)")]
    public void Precedence_and_associativity(string expression, string normalised)
    {
        Assert.Equal(normalised, Normalise(expression));
    }

    [Fact]
    public void Subtraction_is_left_associative()
    {
        var roll = DiceLanguage.Roll("10 - 2 - 3", new ScriptedRandom());

        Assert.Equal(5, roll.Value.Total);
    }

    [Fact]
    public void Multiplication_binds_tighter_than_addition()
    {
        var roll = DiceLanguage.Roll("1 + 2 * 3", new ScriptedRandom());

        Assert.Equal(7, roll.Value.Total);
    }

    [Theory]
    [InlineData("", 0, 0)]
    [InlineData("   ", 0, 0)]
    [InlineData("1d20 +", 6, 0)]
    [InlineData("1d20 + ", 7, 0)]
    [InlineData("1d20 + * 3", 7, 1)]
    [InlineData("1d20)", 4, 1)]
    [InlineData("1d20 3", 5, 1)]
    [InlineData("adv(1d20", 8, 0)]
    [InlineData("adv 1d20", 4, 1)]
    [InlineData("adv()", 4, 1)]
    [InlineData("max(1d4)", 7, 1)]
    [InlineData("10d", 3, 0)]
    [InlineData("-1d4", 0, 1)]
    [InlineData("1d20 + x", 7, 1)]
    [InlineData("1.5", 1, 1)]
    public void Parse_errors_point_at_the_problem(string expression, int position, int length)
    {
        var error = Assert.Single(DiceLanguage.Check(expression).Error);

        Assert.Equal(DiceErrorCodes.ParseError, error.Code);
        Assert.Equal((position, length), (error.Position, error.Length));
    }

    [Fact]
    public void Parse_error_names_the_operator()
    {
        var error = Assert.Single(DiceLanguage.Check("1d20 +").Error);

        Assert.Contains("'+'", error.Message);
    }

    [Fact]
    public void Null_is_a_parse_error_not_an_exception()
    {
        var error = Assert.Single(DiceLanguage.Check(null).Error);

        Assert.Equal(DiceErrorCodes.ParseError, error.Code);
    }

    private static string Normalise(string expression)
    {
        var result = DiceLanguage.Check(expression);
        Assert.True(result.IsSuccess, result.IsFailure ? Describe(result.Error) : "");
        return EvaluationFormatter.Normalise(result.Value);
    }

    internal static string Describe(IEnumerable<DiceError> errors) =>
        string.Join("; ", errors.Select(e => $"{e.Code} '{e.Message}' @{e.Position}+{e.Length}"));
}
