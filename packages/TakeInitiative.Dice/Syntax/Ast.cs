namespace TakeInitiative.Dice.Syntax;

/// <summary>
/// An untyped node, straight from the parser. Every node carries its source span
/// so the type checker can point errors at the offending sub-expression.
/// </summary>
/// <remarks>
/// Values are <see cref="long"/> so an out-of-range literal survives parsing and is
/// reported by the limit checks rather than failing as a confusing parse error.
/// </remarks>
public abstract record DiceNode(int Position, int Length);

public sealed record NumberNode(long Value, int Position, int Length) : DiceNode(Position, Length);

/// <summary><c>NdS</c>. <c>d20</c> parses with <see cref="Count"/> 1.</summary>
public sealed record DiceLiteralNode(long Count, long Sides, int Position, int Length) : DiceNode(Position, Length);

/// <summary><c>kh&lt;n&gt;(e)</c> / <c>kl&lt;n&gt;(e)</c>. <c>kh(e)</c> parses with <see cref="Keep"/> 1.</summary>
public sealed record KeepNode(KeepKind Kind, long Keep, DiceNode Operand, int Position, int Length) : DiceNode(Position, Length);

/// <summary><c>adv(e)</c> / <c>dis(e)</c>.</summary>
public sealed record AdvantageNode(AdvantageKind Kind, DiceNode Operand, int Position, int Length) : DiceNode(Position, Length);

/// <summary><c>min(a, b, ...)</c> / <c>max(a, b, ...)</c>, always two or more arguments.</summary>
public sealed record ExtremumNode(ExtremumKind Kind, IReadOnlyList<DiceNode> Arguments, int Position, int Length) : DiceNode(Position, Length);

public sealed record BinaryNode(BinaryOperator Operator, DiceNode Left, DiceNode Right, int Position, int Length) : DiceNode(Position, Length);

/// <summary>
/// d20's postfix <c>2d20kh1</c>. Not part of the language: it is parsed only so the
/// type checker can say exactly how to rewrite it.
/// </summary>
public sealed record LegacyKeepNode(DiceLiteralNode Dice, KeepKind Kind, long Keep, int Position, int Length) : DiceNode(Position, Length);

public enum KeepKind { Highest, Lowest }

public enum AdvantageKind { Advantage, Disadvantage }

public enum ExtremumKind { Min, Max }

public enum BinaryOperator { Add, Subtract, Multiply, Divide }

public static class SyntaxNames
{
    public static string Of(KeepKind kind) => kind == KeepKind.Highest ? "kh" : "kl";

    public static string Of(AdvantageKind kind) => kind == AdvantageKind.Advantage ? "adv" : "dis";

    public static string Of(ExtremumKind kind) => kind == ExtremumKind.Min ? "min" : "max";

    public static char Of(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => '+',
        BinaryOperator.Subtract => '-',
        BinaryOperator.Multiply => '*',
        BinaryOperator.Divide => '/',
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    public static int Precedence(BinaryOperator op) =>
        op is BinaryOperator.Multiply or BinaryOperator.Divide ? 2 : 1;
}
