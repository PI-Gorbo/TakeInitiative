using CSharpFunctionalExtensions;
using Parlot;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace TakeInitiative.Dice.Syntax;

/// <summary>
/// Parses the grammar in docs/roadmap/08-dice-language-spec.md §2 into an untyped
/// <see cref="DiceNode"/> tree. Parse errors stop at the first problem; everything
/// semantic is left to the type checker.
/// </summary>
public static class DiceParser
{
    private const string ExpectedOperand = "expected a number, dice or function";

    private static readonly Parser<DiceNode> Expression = BuildGrammar();

    public static Result<DiceNode, DiceError> Parse(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return new DiceError(DiceErrorCodes.ParseError, "enter a roll, like 1d20 + 2", 0, 0);
        }

        var context = new ParseContext(new Scanner(source));
        if (!Expression.TryParse(context, out var node, out var error))
        {
            return ErrorAt(source, error?.Position.Offset ?? 0, error?.Message ?? ExpectedOperand);
        }

        var end = SkipWhiteSpace(source, context.Scanner.Cursor.Offset);
        if (end < source.Length)
        {
            return ErrorAt(source, end, $"unexpected '{source[end]}'");
        }

        return node;
    }

    private static DiceError ErrorAt(string source, int offset, string message)
    {
        // Parlot reports the position before any whitespace it skipped; point at
        // the character that actually caused the problem.
        var position = SkipWhiteSpace(source, offset);
        return new DiceError(DiceErrorCodes.ParseError, message, position, position < source.Length ? 1 : 0);
    }

    private static int SkipWhiteSpace(string source, int offset)
    {
        while (offset < source.Length && char.IsWhiteSpace(source[offset]))
        {
            offset++;
        }
        return offset;
    }

    private static Parser<DiceNode> BuildGrammar()
    {
        var expression = Deferred<DiceNode>();

        var integer = Terms.Pattern(char.IsAsciiDigit).Then(ParseInteger);
        var openParen = Terms.Char('(');
        Parser<char> CloseParen(string context) => Terms.Char(')').ElseError($"expected ')' to close {context}");
        Parser<char> OpenParenAfter(string name) => openParen.ElseError($"expected '(' after {name}");

        var number = Spanned(integer, (value, position, length) => (DiceNode)new NumberNode(value, position, length));

        var diceLiteral = Spanned(
            ZeroOrOne(integer, 1)
                .And(Terms.Text("d", caseInsensitive: true))
                .And(integer.ElseError("expected the number of sides after 'd', like 1d20")),
            (x, position, length) => new DiceLiteralNode(x.Item1, x.Item3, position, length));

        var keepKind = OneOf(
            Terms.Text("kh", caseInsensitive: true).Then(KeepKind.Highest),
            Terms.Text("kl", caseInsensitive: true).Then(KeepKind.Lowest));

        // d20's `2d20kh1`. Tried before a plain dice literal so the suffix is not
        // left dangling as "unexpected 'k'".
        var legacyKeep = Spanned(
            diceLiteral.And(keepKind).And(ZeroOrOne(integer, 1)),
            (x, position, length) => (DiceNode)new LegacyKeepNode(x.Item1, x.Item2, x.Item3, position, length));

        var dice = diceLiteral.Then<DiceNode>(x => x);

        var requiredExpression = expression.ElseError(ExpectedOperand);

        var keep = Spanned(
            keepKind
                .And(ZeroOrOne(integer, 1))
                .AndSkip(OpenParenAfter("kh / kl"))
                .And(requiredExpression)
                .AndSkip(CloseParen("kh( / kl(")),
            (x, position, length) => (DiceNode)new KeepNode(x.Item1, x.Item2, x.Item3, position, length));

        Parser<DiceNode> Advantage(string name, AdvantageKind kind) => Spanned(
            Terms.Text(name, caseInsensitive: true)
                .SkipAnd(OpenParenAfter(name))
                .SkipAnd(requiredExpression)
                .AndSkip(CloseParen($"{name}(")),
            (operand, position, length) => (DiceNode)new AdvantageNode(kind, operand, position, length));

        Parser<DiceNode> Extremum(string name, ExtremumKind kind) => Spanned(
            Terms.Text(name, caseInsensitive: true)
                .SkipAnd(OpenParenAfter(name))
                .SkipAnd(requiredExpression)
                .And(OneOrMany(Terms.Char(',').SkipAnd(requiredExpression))
                    .ElseError($"{name}() needs at least two things to compare, like {name}(1d4, 2)"))
                .AndSkip(CloseParen($"{name}(")),
            (x, position, length) => (DiceNode)new ExtremumNode(kind, [x.Item1, .. x.Item2], position, length));

        var parenthesised = openParen
            .SkipAnd(requiredExpression)
            .AndSkip(CloseParen("the bracket"));

        // Order matters: `dis` must be tried before the `d` of a dice literal, and
        // the legacy suffix before the plain literal.
        var atom = OneOf(
            Advantage("adv", AdvantageKind.Advantage),
            Advantage("dis", AdvantageKind.Disadvantage),
            Extremum("min", ExtremumKind.Min),
            Extremum("max", ExtremumKind.Max),
            keep,
            parenthesised,
            legacyKeep,
            dice,
            number);

        var product = BinaryLevel(atom, ('*', BinaryOperator.Multiply), ('/', BinaryOperator.Divide));
        var sum = BinaryLevel(product, ('+', BinaryOperator.Add), ('-', BinaryOperator.Subtract));

        expression.Parser = sum;
        return expression;
    }

    /// <summary>
    /// One left-associative precedence level. Written by hand rather than with
    /// <c>LeftAssociative</c> so a missing operand gets a message naming the operator.
    /// </summary>
    private static Parser<DiceNode> BinaryLevel(Parser<DiceNode> operand, params (char Symbol, BinaryOperator Operator)[] operators)
    {
        var tails = operators
            .Select(op => Terms.Char(op.Symbol)
                .SkipAnd(operand.ElseError($"{ExpectedOperand} after '{op.Symbol}'"))
                .Then(right => (op.Operator, Right: right)))
            .ToArray();

        return operand
            .And(ZeroOrMany(OneOf(tails)))
            .Then(x => x.Item2.Aggregate(x.Item1, (left, tail) =>
            {
                var end = tail.Right.Position + tail.Right.Length;
                return new BinaryNode(tail.Operator, left, tail.Right, left.Position, end - left.Position);
            }));
    }

    private static Parser<TNode> Spanned<T, TNode>(Parser<T> inner, Func<T, int, int, TNode> build) =>
        new SpannedParser<T, TNode>(inner, build);

    /// <summary>
    /// Records a node's source span from the scanner cursor. Parlot's own
    /// <c>Then((ctx, start, end, value) => ...)</c> reports 0 for a span that starts
    /// or ends with an optional that matched nothing (<c>d20</c>, <c>2d20kh</c>).
    /// </summary>
    private sealed class SpannedParser<T, TNode>(Parser<T> inner, Func<T, int, int, TNode> build) : Parser<TNode>
    {
        public override bool Parse(ParseContext context, ref ParseResult<TNode> result)
        {
            context.EnterParser(this);
            var cursor = context.Scanner.Cursor;
            var before = cursor.Position;

            context.SkipWhiteSpace();
            var start = cursor.Offset;

            var innerResult = new ParseResult<T>();
            if (inner.Parse(context, ref innerResult))
            {
                var end = cursor.Offset;
                result.Set(start, end, build(innerResult.Value, start, end - start));
                context.ExitParser(this);
                return true;
            }

            cursor.ResetPosition(before);
            context.ExitParser(this);
            return false;
        }
    }

    private static long ParseInteger(TextSpan digits) =>
        // Anything that overflows a long is far past every limit anyway; clamp it so
        // the limit checks report it instead of the parser choking.
        long.TryParse(digits.Span, out var value) ? value : long.MaxValue;
}
