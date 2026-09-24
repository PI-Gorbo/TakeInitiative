using CSharpFunctionalExtensions;
using TakeInitiative.Dice.Syntax;

namespace TakeInitiative.Dice.Typing;

/// <summary>
/// Applies the typing rules and limits from docs/roadmap/08-dice-language-spec.md
/// §3–4. Collects every error rather than stopping at the first.
/// </summary>
public static class DiceTypeChecker
{
    private sealed record VisitOutcome(TypedExpression Expression, int DiceRolled);

    public static Result<TypedExpression, DiceError[]> Check(DiceNode root)
    {
        var outcome = Visit(root);
        if (outcome.IsFailure)
        {
            return outcome.Error.OrderBy(e => e.Position).ToArray();
        }

        var totalDice = outcome.Value.DiceRolled;
        if (totalDice > DiceLimits.MaxTotalDice)
        {
            return new[] { Error(DiceErrorCodes.TooManyDice,
                $"that rolls {totalDice} dice; the most one roll can use is {DiceLimits.MaxTotalDice}", root) };
        }

        return outcome.Value.Expression;
    }

    private static Result<VisitOutcome, DiceError[]> Visit(DiceNode node) => node switch
    {
        NumberNode n => VisitNumber(n),
        DiceLiteralNode d => VisitDice(d),
        KeepNode k => VisitKeep(k),
        AdvantageNode a => VisitAdvantage(a),
        ExtremumNode e => VisitExtremum(e),
        BinaryNode b => VisitBinary(b),
        LegacyKeepNode l => VisitLegacyKeep(l),
        _ => throw new ArgumentOutOfRangeException(nameof(node), node.GetType().Name, "unknown dice node"),
    };

    private static Result<VisitOutcome, DiceError[]> VisitNumber(NumberNode node)
    {
        if (node.Value > DiceLimits.MaxNumber)
        {
            return new[] { Error(DiceErrorCodes.NumberTooLarge, $"numbers can be at most {DiceLimits.MaxNumber}", node) };
        }
        return new VisitOutcome(new TypedNumber((int)node.Value, node.Position, node.Length), 0);
    }

    private static Result<VisitOutcome, DiceError[]> VisitDice(DiceLiteralNode node)
    {
        List<DiceError> errors = [];
        if (node.Count == 0)
        {
            errors.Add(Error(DiceErrorCodes.DiceCountZero, "roll at least 1 die", node));
        }
        else if (node.Count > DiceLimits.MaxDicePerPool)
        {
            errors.Add(Error(DiceErrorCodes.PoolTooLarge, $"you can roll at most {DiceLimits.MaxDicePerPool} dice at once", node));
        }

        if (node.Sides == 0)
        {
            errors.Add(Error(DiceErrorCodes.DiceSidesZero, "a die needs at least 1 side", node));
        }
        else if (node.Sides > DiceLimits.MaxSides)
        {
            errors.Add(Error(DiceErrorCodes.TooManySides, $"dice can have at most {DiceLimits.MaxSides} sides", node));
        }

        if (errors.Count > 0) return errors.ToArray();

        var count = (int)node.Count;
        return new VisitOutcome(new TypedDice(count, (int)node.Sides, node.Position, node.Length), count);
    }

    private static Result<VisitOutcome, DiceError[]> VisitKeep(KeepNode node)
    {
        var name = SyntaxNames.Of(node.Kind);
        var operand = Visit(node.Operand);

        List<DiceError> errors = [.. ErrorsOf(operand)];
        if (node.Keep < 1)
        {
            errors.Add(Error(DiceErrorCodes.KeepAtLeastOne, "must keep at least 1 die", node));
        }

        if (operand.IsFailure) return errors.ToArray();

        if (operand.Value.Expression.Type is not DiceType.Pool pool)
        {
            errors.Add(Error(DiceErrorCodes.KeepRequiresPool, $"{name}() needs dice to choose from, not a fixed number", node.Operand));
            return errors.ToArray();
        }

        if (errors.Count == 0 && node.Keep > pool.Count)
        {
            var which = node.Kind == KeepKind.Highest ? "highest" : "lowest";
            var dice = pool.Count == 1 ? "1 die" : $"{pool.Count} dice";
            errors.Add(Error(DiceErrorCodes.KeepExceedsPool, $"can't keep the {which} {node.Keep} of only {dice}", node));
        }

        if (errors.Count > 0) return errors.ToArray();

        var keep = (int)node.Keep;
        var typed = new TypedKeep(node.Kind, keep, operand.Value.Expression, new DiceType.Pool(keep, pool.Sides), node.Position, node.Length);
        return new VisitOutcome(typed, operand.Value.DiceRolled);
    }

    private static Result<VisitOutcome, DiceError[]> VisitAdvantage(AdvantageNode node)
    {
        var name = SyntaxNames.Of(node.Kind);
        var operand = Visit(node.Operand);
        if (operand.IsFailure) return operand.Error;

        if (operand.Value.Expression is not TypedDice { Count: 1, Sides: 20 } die)
        {
            var hint = operand.Value.Expression is TypedDice { Sides: 20 } d
                ? $" — or use kh1({d.Count}d20) to keep 1 of {d.Count}"
                : "";
            return new[] { Error(DiceErrorCodes.AdvRequiresD20, $"{name}() only works on a single d20 — try {name}(1d20){hint}", node.Operand) };
        }

        // adv rolls its die twice; the operand visit has already counted one of them.
        return new VisitOutcome(new TypedAdvantage(node.Kind, die, node.Position, node.Length), operand.Value.DiceRolled + 1);
    }

    private static Result<VisitOutcome, DiceError[]> VisitExtremum(ExtremumNode node)
    {
        var arguments = node.Arguments.Select(Visit).ToList();

        var errors = arguments.SelectMany(ErrorsOf).ToArray();
        if (errors.Length > 0) return errors;

        var typed = new TypedExtremum(node.Kind, arguments.Select(a => a.Value.Expression).ToList(), node.Position, node.Length);
        return new VisitOutcome(typed, arguments.Sum(a => a.Value.DiceRolled));
    }

    private static Result<VisitOutcome, DiceError[]> VisitBinary(BinaryNode node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);

        List<DiceError> errors = [.. ErrorsOf(left), .. ErrorsOf(right)];
        if (node is { Operator: BinaryOperator.Divide, Right: NumberNode { Value: 0 } })
        {
            errors.Add(Error(DiceErrorCodes.DivideByZero, "can't divide by zero", node.Right));
        }

        if (errors.Count > 0) return errors.ToArray();

        var typed = new TypedBinary(node.Operator, left.Value.Expression, right.Value.Expression, node.Position, node.Length);
        return new VisitOutcome(typed, left.Value.DiceRolled + right.Value.DiceRolled);
    }

    private static Result<VisitOutcome, DiceError[]> VisitLegacyKeep(LegacyKeepNode node)
    {
        var rewrite = $"{SyntaxNames.Of(node.Kind)}{node.Keep}({node.Dice.Count}d{node.Dice.Sides})";
        return new[] { Error(DiceErrorCodes.LegacyKeepSyntax, $"write this as {rewrite}", node) };
    }

    private static DiceError[] ErrorsOf(Result<VisitOutcome, DiceError[]> result) =>
        result.IsFailure ? result.Error : [];

    private static DiceError Error(string code, string message, DiceNode at) =>
        new(code, message, at.Position, at.Length);
}
