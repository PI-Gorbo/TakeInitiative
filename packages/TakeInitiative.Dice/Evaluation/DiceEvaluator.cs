using CSharpFunctionalExtensions;
using TakeInitiative.Dice.Syntax;
using TakeInitiative.Dice.Typing;

namespace TakeInitiative.Dice.Evaluation;

/// <summary>The result of evaluating one node, kept so the formatter can show every die.</summary>
public abstract record Evaluated(TypedExpression Source, int Total);

/// <summary>
/// A maximal pool subtree: a dice literal, keep or advantage, rolled as one unit.
/// Both lists are sorted descending.
/// </summary>
public sealed record EvaluatedPool(TypedExpression Source, IReadOnlyList<int> Kept, IReadOnlyList<int> Dropped)
    : Evaluated(Source, Kept.Sum());

public sealed record EvaluatedNumber(TypedNumber Number) : Evaluated(Number, Number.Value);

public sealed record EvaluatedExtremum(TypedExtremum Extremum, IReadOnlyList<Evaluated> Arguments, int Total)
    : Evaluated(Extremum, Total);

public sealed record EvaluatedBinary(TypedBinary Binary, Evaluated Left, Evaluated Right, int Total)
    : Evaluated(Binary, Total);

/// <summary>
/// Rolls a <see cref="TypedExpression"/>. The <see cref="Random"/> is injected so tests
/// are deterministic; production passes <see cref="Random.Shared"/>.
/// </summary>
public sealed class DiceEvaluator(Random random)
{
    public Result<Evaluated, DiceError> Evaluate(TypedExpression expression)
    {
        try
        {
            return Visit(expression);
        }
        catch (EvaluationException ex)
        {
            return ex.Error;
        }
    }

    private Evaluated Visit(TypedExpression node) => node switch
    {
        TypedNumber n => new EvaluatedNumber(n),
        TypedDice or TypedKeep or TypedAdvantage => VisitPool(node),
        TypedExtremum e => VisitExtremum(e),
        TypedBinary b => VisitBinary(b),
        _ => throw new ArgumentOutOfRangeException(nameof(node), node.GetType().Name, "unknown typed node"),
    };

    private EvaluatedPool VisitPool(TypedExpression node)
    {
        var (kept, dropped) = RollPool(node);
        return new EvaluatedPool(node, Descending(kept), Descending(dropped));
    }

    private (List<int> Kept, List<int> Dropped) RollPool(TypedExpression node)
    {
        switch (node)
        {
            case TypedDice dice:
                return (Enumerable.Range(0, dice.Count).Select(_ => RollDie(dice.Sides)).ToList(), []);

            case TypedAdvantage adv:
                var rolls = new List<int> { RollDie(adv.Die.Sides), RollDie(adv.Die.Sides) };
                return Select(adv.Kind == AdvantageKind.Advantage ? KeepKind.Highest : KeepKind.Lowest, 1, rolls, []);

            case TypedKeep keep:
                // The outer keep selects from what the inner one kept; anything the
                // inner keep dropped stays dropped.
                var (innerKept, innerDropped) = RollPool(keep.Operand);
                return Select(keep.Kind, keep.Keep, innerKept, innerDropped);

            default:
                throw new ArgumentOutOfRangeException(nameof(node), node.GetType().Name, "not a pool");
        }
    }

    private static (List<int> Kept, List<int> Dropped) Select(KeepKind kind, int keep, List<int> candidates, List<int> alreadyDropped)
    {
        var ordered = kind == KeepKind.Highest
            ? candidates.OrderByDescending(x => x).ToList()
            : candidates.OrderBy(x => x).ToList();

        return (ordered.Take(keep).ToList(), [.. ordered.Skip(keep), .. alreadyDropped]);
    }

    private EvaluatedExtremum VisitExtremum(TypedExtremum node)
    {
        var arguments = node.Arguments.Select(Visit).ToList();
        var total = node.Kind == ExtremumKind.Min
            ? arguments.Min(a => a.Total)
            : arguments.Max(a => a.Total);
        return new EvaluatedExtremum(node, arguments, total);
    }

    private EvaluatedBinary VisitBinary(TypedBinary node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);

        if (node.Operator == BinaryOperator.Divide && right.Total == 0)
        {
            throw new EvaluationException(new DiceError(DiceErrorCodes.DivideByZero,
                "that rolled a zero to divide by", node.Right.Position, node.Right.Length));
        }

        try
        {
            var total = node.Operator switch
            {
                BinaryOperator.Add => checked(left.Total + right.Total),
                BinaryOperator.Subtract => checked(left.Total - right.Total),
                BinaryOperator.Multiply => checked(left.Total * right.Total),
                BinaryOperator.Divide => FloorDivide(left.Total, right.Total),
                _ => throw new ArgumentOutOfRangeException(nameof(node), node.Operator, "unknown operator"),
            };
            return new EvaluatedBinary(node, left, right, total);
        }
        catch (OverflowException)
        {
            throw new EvaluationException(new DiceError(DiceErrorCodes.ResultOutOfRange,
                "that result is too big to count", node.Position, node.Length));
        }
    }

    /// <summary>D&amp;D rounds down, including for negative results.</summary>
    private static int FloorDivide(int dividend, int divisor)
    {
        var quotient = checked(dividend / divisor);
        return dividend % divisor != 0 && (dividend < 0) != (divisor < 0) ? quotient - 1 : quotient;
    }

    private int RollDie(int sides) => random.Next(1, sides + 1);

    private static List<int> Descending(List<int> values) => [.. values.OrderByDescending(x => x)];

    private sealed class EvaluationException(DiceError error) : Exception(error.Message)
    {
        public DiceError Error { get; } = error;
    }
}
