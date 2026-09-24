using CSharpFunctionalExtensions;
using TakeInitiative.Dice.Syntax;

namespace TakeInitiative.Dice.Typing;

/// <summary>
/// Applies the typing rules and limits from docs/roadmap/08-dice-language-spec.md
/// §3–4. Collects every error rather than stopping at the first.
/// </summary>
public sealed class DiceTypeChecker
{
    private readonly List<DiceError> _errors = [];
    private int _totalDice;

    public static Result<TypedExpression, DiceError[]> Check(DiceNode root)
    {
        var checker = new DiceTypeChecker();
        var typed = checker.Visit(root);

        if (checker._totalDice > DiceLimits.MaxTotalDice)
        {
            checker.Error(DiceErrorCodes.TooManyDice,
                $"that rolls {checker._totalDice} dice; the most one roll can use is {DiceLimits.MaxTotalDice}", root);
        }

        if (checker._errors.Count > 0 || typed is null)
        {
            return checker._errors.OrderBy(e => e.Position).ToArray();
        }

        return typed;
    }

    /// <returns>The typed node, or null when this subtree has an error.</returns>
    private TypedExpression? Visit(DiceNode node) => node switch
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

    private TypedNumber? VisitNumber(NumberNode node)
    {
        if (node.Value > DiceLimits.MaxNumber)
        {
            Error(DiceErrorCodes.NumberTooLarge, $"numbers can be at most {DiceLimits.MaxNumber}", node);
            return null;
        }
        return new TypedNumber((int)node.Value, node.Position, node.Length);
    }

    private TypedDice? VisitDice(DiceLiteralNode node)
    {
        var ok = true;
        if (node.Count == 0)
        {
            ok = Error(DiceErrorCodes.DiceCountZero, "roll at least 1 die", node);
        }
        else if (node.Count > DiceLimits.MaxDicePerPool)
        {
            ok = Error(DiceErrorCodes.PoolTooLarge, $"you can roll at most {DiceLimits.MaxDicePerPool} dice at once", node);
        }

        if (node.Sides == 0)
        {
            ok = Error(DiceErrorCodes.DiceSidesZero, "a die needs at least 1 side", node);
        }
        else if (node.Sides > DiceLimits.MaxSides)
        {
            ok = Error(DiceErrorCodes.TooManySides, $"dice can have at most {DiceLimits.MaxSides} sides", node);
        }

        if (!ok) return null;

        _totalDice += (int)node.Count;
        return new TypedDice((int)node.Count, (int)node.Sides, node.Position, node.Length);
    }

    private TypedKeep? VisitKeep(KeepNode node)
    {
        var name = SyntaxNames.Of(node.Kind);
        var operand = Visit(node.Operand);
        var ok = true;

        if (node.Keep < 1)
        {
            ok = Error(DiceErrorCodes.KeepAtLeastOne, "must keep at least 1 die", node);
        }

        if (operand is null) return null;

        if (operand.Type is not DiceType.Pool pool)
        {
            Error(DiceErrorCodes.KeepRequiresPool, $"{name}() needs dice to choose from, not a fixed number", node.Operand);
            return null;
        }

        if (ok && node.Keep > pool.Count)
        {
            var which = node.Kind == KeepKind.Highest ? "highest" : "lowest";
            var dice = pool.Count == 1 ? "1 die" : $"{pool.Count} dice";
            ok = Error(DiceErrorCodes.KeepExceedsPool, $"can't keep the {which} {node.Keep} of only {dice}", node);
        }

        if (!ok) return null;

        var keep = (int)node.Keep;
        return new TypedKeep(node.Kind, keep, operand, new DiceType.Pool(keep, pool.Sides), node.Position, node.Length);
    }

    private TypedAdvantage? VisitAdvantage(AdvantageNode node)
    {
        var name = SyntaxNames.Of(node.Kind);
        var operand = Visit(node.Operand);
        if (operand is null) return null;

        if (operand is not TypedDice { Count: 1, Sides: 20 } die)
        {
            var hint = operand is TypedDice { Sides: 20 } d
                ? $" — or use kh1({d.Count}d20) to keep 1 of {d.Count}"
                : "";
            Error(DiceErrorCodes.AdvRequiresD20, $"{name}() only works on a single d20 — try {name}(1d20){hint}", node.Operand);
            return null;
        }

        // adv rolls its die twice; the operand visit has already counted one of them.
        _totalDice += 1;
        return new TypedAdvantage(node.Kind, die, node.Position, node.Length);
    }

    private TypedExtremum? VisitExtremum(ExtremumNode node)
    {
        var arguments = node.Arguments.Select(Visit).ToList();
        if (arguments.Any(a => a is null)) return null;

        return new TypedExtremum(node.Kind, arguments!, node.Position, node.Length);
    }

    private TypedBinary? VisitBinary(BinaryNode node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);

        if (node is { Operator: BinaryOperator.Divide, Right: NumberNode { Value: 0 } })
        {
            Error(DiceErrorCodes.DivideByZero, "can't divide by zero", node.Right);
            return null;
        }

        if (left is null || right is null) return null;

        return new TypedBinary(node.Operator, left, right, node.Position, node.Length);
    }

    private TypedExpression? VisitLegacyKeep(LegacyKeepNode node)
    {
        var rewrite = $"{SyntaxNames.Of(node.Kind)}{node.Keep}({node.Dice.Count}d{node.Dice.Sides})";
        Error(DiceErrorCodes.LegacyKeepSyntax, $"write this as {rewrite}", node);
        return null;
    }

    /// <returns>Always false, so callers can write <c>ok = Error(...)</c>.</returns>
    private bool Error(string code, string message, DiceNode at)
    {
        _errors.Add(new DiceError(code, message, at.Position, at.Length));
        return false;
    }
}
