using System.Text;
using TakeInitiative.Dice.Syntax;
using TakeInitiative.Dice.Typing;

namespace TakeInitiative.Dice.Evaluation;

/// <summary>
/// Renders expressions from the tree rather than the input string, so <c>1D20+3</c>
/// comes out as <c>1d20 + 3</c>. Parentheses are emitted where precedence needs
/// them, not where the user typed them. See spec §5.
/// </summary>
public static class EvaluationFormatter
{
    /// <summary>e.g. <c>adv(1d20) [18, ~7] + 5 = 23</c>.</summary>
    public static string Format(Evaluated evaluated)
    {
        var builder = new StringBuilder();
        Write(builder, evaluated);
        return builder.Append(" = ").Append(evaluated.Total).ToString();
    }

    /// <summary>The normalised source of a checked expression, e.g. <c>kh1(2d20) + 3</c>.</summary>
    public static string Normalise(TypedExpression expression)
    {
        var builder = new StringBuilder();
        WriteSource(builder, expression);
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, Evaluated evaluated)
    {
        switch (evaluated)
        {
            case EvaluatedPool pool:
                WriteSource(builder, pool.Source);
                builder.Append(" [")
                    .AppendJoin(", ", pool.Kept.Select(x => x.ToString()).Concat(pool.Dropped.Select(x => $"~{x}")))
                    .Append(']');
                break;

            case EvaluatedNumber number:
                builder.Append(number.Total);
                break;

            case EvaluatedExtremum extremum:
                builder.Append(SyntaxNames.Of(extremum.Extremum.Kind)).Append('(');
                for (var i = 0; i < extremum.Arguments.Count; i++)
                {
                    if (i > 0) builder.Append(", ");
                    Write(builder, extremum.Arguments[i]);
                }
                builder.Append(')');
                break;

            case EvaluatedBinary binary:
                var op = binary.Binary.Operator;
                WriteOperand(builder, binary.Left, op, isRight: false, b => Write(b, binary.Left));
                builder.Append(' ').Append(SyntaxNames.Of(op)).Append(' ');
                WriteOperand(builder, binary.Right, op, isRight: true, b => Write(b, binary.Right));
                break;
        }
    }

    private static void WriteSource(StringBuilder builder, TypedExpression expression)
    {
        switch (expression)
        {
            case TypedNumber number:
                builder.Append(number.Value);
                break;

            case TypedDice dice:
                builder.Append(dice.Count).Append('d').Append(dice.Sides);
                break;

            case TypedKeep keep:
                builder.Append(SyntaxNames.Of(keep.Kind)).Append(keep.Keep).Append('(');
                WriteSource(builder, keep.Operand);
                builder.Append(')');
                break;

            case TypedAdvantage adv:
                builder.Append(SyntaxNames.Of(adv.Kind)).Append('(');
                WriteSource(builder, adv.Die);
                builder.Append(')');
                break;

            case TypedExtremum extremum:
                builder.Append(SyntaxNames.Of(extremum.Kind)).Append('(');
                for (var i = 0; i < extremum.Arguments.Count; i++)
                {
                    if (i > 0) builder.Append(", ");
                    WriteSource(builder, extremum.Arguments[i]);
                }
                builder.Append(')');
                break;

            case TypedBinary binary:
                WriteOperand(builder, binary.Left, binary.Operator, isRight: false, b => WriteSource(b, binary.Left));
                builder.Append(' ').Append(SyntaxNames.Of(binary.Operator)).Append(' ');
                WriteOperand(builder, binary.Right, binary.Operator, isRight: true, b => WriteSource(b, binary.Right));
                break;
        }
    }

    private static void WriteOperand(StringBuilder builder, object operand, BinaryOperator parent, bool isRight, Action<StringBuilder> write)
    {
        var child = operand switch
        {
            EvaluatedBinary b => b.Binary.Operator,
            TypedBinary b => b.Operator,
            _ => (BinaryOperator?)null,
        };

        // Everything is left-associative, so a right operand at the same precedence
        // needs brackets to keep its grouping: 10 - (2 - 3).
        var needsParens = child is { } c && (isRight
            ? SyntaxNames.Precedence(c) <= SyntaxNames.Precedence(parent)
            : SyntaxNames.Precedence(c) < SyntaxNames.Precedence(parent));

        if (needsParens) builder.Append('(');
        write(builder);
        if (needsParens) builder.Append(')');
    }
}
