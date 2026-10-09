using CSharpFunctionalExtensions;
using TakeInitiative.Dice.Evaluation;
using TakeInitiative.Dice.Syntax;
using TakeInitiative.Dice.Typing;

namespace TakeInitiative.Dice;

/// <summary>
/// The dice language: <c>source ─parse─▶ DiceNode ─check─▶ TypedExpression ─evaluate─▶ DiceRoll</c>.
/// See docs/roadmap/08-dice-language-spec.md.
/// </summary>
public static class DiceLanguage
{
    /// <summary>Validates an expression without rolling anything.</summary>
    public static Result<TypedExpression, DiceError[]> Check(string? expression)
    {
        var source = expression ?? "";

        // Both are checked on the raw text so a pathological input never reaches
        // the parser's recursion.
        if (source.Length > DiceLimits.MaxExpressionLength)
        {
            return new[]
            {
                new DiceError(DiceErrorCodes.ExpressionTooLong,
                    $"that roll is too long — keep it under {DiceLimits.MaxExpressionLength} characters",
                    DiceLimits.MaxExpressionLength, source.Length - DiceLimits.MaxExpressionLength),
            };
        }

        if (FindTooDeep(source) is { } tooDeep)
        {
            return new[]
            {
                new DiceError(DiceErrorCodes.ExpressionTooDeep,
                    $"that roll nests too deeply — at most {DiceLimits.MaxNestingDepth} brackets", tooDeep, 1),
            };
        }

        return DiceParser.Parse(source)
            .MapError(error => new[] { error })
            .Bind(DiceTypeChecker.Check);
    }

    /// <summary>Checks then rolls an expression.</summary>
    public static Result<DiceRoll, DiceError[]> Roll(string? expression, Random random) =>
        Check(expression).Bind(typed => new DiceEvaluator(random)
            .Evaluate(typed)
            .MapError(error => new[] { error })
            .Map(evaluated => new DiceRoll(evaluated.Total, expression!, EvaluationFormatter.Format(evaluated))));

    /// <summary>
    /// A single d20, built directly rather than through <see cref="Roll"/>, so it can
    /// never fail — initiative tie-breaks call this in a loop.
    /// </summary>
    public static DiceRoll RollD20(Random random)
    {
        var value = random.Next(1, 21);
        return new DiceRoll(value, "1d20", $"1d20 [{value}] = {value}");
    }

    /// <returns>The offset of the first bracket past the depth limit, if any.</returns>
    private static int? FindTooDeep(string source)
    {
        var depth = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] == '(' && ++depth > DiceLimits.MaxNestingDepth) return i;
            if (source[i] == ')') depth--;
        }
        return null;
    }
}
