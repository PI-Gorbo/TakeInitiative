using TakeInitiative.Dice.Syntax;

namespace TakeInitiative.Dice.Typing;

/// <summary>
/// A checked expression. Every value is within <see cref="DiceLimits"/> and every
/// node carries its resolved <see cref="DiceType"/>, so the evaluator never
/// re-derives anything.
/// </summary>
public abstract record TypedExpression(DiceType Type, int Position, int Length);

public sealed record TypedNumber(int Value, int Position, int Length)
    : TypedExpression(DiceType.ScalarType, Position, Length);

public sealed record TypedDice(int Count, int Sides, int Position, int Length)
    : TypedExpression(new DiceType.Pool(Count, Sides), Position, Length);

public sealed record TypedKeep(KeepKind Kind, int Keep, TypedExpression Operand, DiceType.Pool PoolType, int Position, int Length)
    : TypedExpression(PoolType, Position, Length);

/// <summary><c>adv(1d20)</c> / <c>dis(1d20)</c>. The operand is always a single d20.</summary>
public sealed record TypedAdvantage(AdvantageKind Kind, TypedDice Die, int Position, int Length)
    : TypedExpression(Die.Type, Position, Length);

public sealed record TypedExtremum(ExtremumKind Kind, IReadOnlyList<TypedExpression> Arguments, int Position, int Length)
    : TypedExpression(DiceType.ScalarType, Position, Length);

public sealed record TypedBinary(BinaryOperator Operator, TypedExpression Left, TypedExpression Right, int Position, int Length)
    : TypedExpression(DiceType.ScalarType, Position, Length);
