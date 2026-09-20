namespace Motiv.Serialization.Expressions;

/// <summary>
/// Recursive descent over the grammar in the design doc. One problem ends the parse: after a
/// syntax error the rest of the text cannot be trusted to mean anything, and a cascade of
/// follow-on errors would bury the one that matters.
/// </summary>
internal sealed class LeafParser
{
    private readonly IReadOnlyList<LeafToken> _tokens;
    private int _index;

    private LeafParser(IReadOnlyList<LeafToken> tokens)
    {
        _tokens = tokens;
    }

    public static LeafNode? Parse(string text, List<LeafProblem> problems)
    {
        var tokens = LeafTokenizer.Tokenize(text, problems);
        if (problems.Count > 0)
            return null;
        if (tokens.Count == 1)
        {
            problems.Add(new LeafProblem(RuleErrorCode.InvalidExpression, "empty expression", 0, 0));
            return null;
        }

        var parser = new LeafParser(tokens);
        try
        {
            var node = parser.Or();
            var rest = parser.Peek();
            if (rest.Kind != LeafTokenKind.End)
                throw new SyntaxError($"unexpected '{rest.Text}'", rest);
            return node;
        }
        catch (SyntaxError error)
        {
            problems.Add(new LeafProblem(RuleErrorCode.InvalidExpression, error.Message, error.Token.Start, error.Token.End));
            return null;
        }
    }

    private sealed class SyntaxError(string message, LeafToken token) : Exception(message)
    {
        public LeafToken Token { get; } = token;
    }

    private LeafToken Peek(int offset = 0) =>
        _index + offset < _tokens.Count ? _tokens[_index + offset] : _tokens[_tokens.Count - 1];

    private LeafToken Next()
    {
        var token = Peek();
        if (token.Kind == LeafTokenKind.End)
            throw new SyntaxError("unexpected end of expression", token);
        _index++;
        return token;
    }

    private bool At(string text) => Peek().Text == text && Peek().Kind != LeafTokenKind.String;

    private LeafToken Expect(string text)
    {
        var token = Peek();
        if (token.Kind == LeafTokenKind.End)
            throw new SyntaxError("unexpected end of expression", token);
        if (token.Text != text)
            throw new SyntaxError($"expected '{text}'", token);
        return Next();
    }

    private LeafNode BinaryLevel(string[] operators, Func<LeafNode> below, bool once = false)
    {
        var left = below();
        while (Peek().Kind == LeafTokenKind.Operator && Array.IndexOf(operators, Peek().Text) >= 0)
        {
            var op = Next().Text;
            var right = below();
            left = new Binary(op, left, right, left.Start, right.End);
            if (once) break;
        }
        return left;
    }

    private LeafNode Or() => BinaryLevel(["||"], And);
    private LeafNode And() => BinaryLevel(["&&"], Equality);
    private LeafNode Equality() => BinaryLevel(["==", "!="], Comparison, once: true);
    private LeafNode Comparison() => BinaryLevel(["<", "<=", ">", ">="], Additive, once: true);
    private LeafNode Additive() => BinaryLevel(["+", "-"], Multiplicative);
    private LeafNode Multiplicative() => BinaryLevel(["*", "/"], UnaryLevel);

    private LeafNode UnaryLevel()
    {
        if (At("!") || At("-"))
        {
            var op = Next();
            var operand = UnaryLevel();
            return new Unary(op.Text, operand, op.Start, operand.End);
        }
        return Postfix();
    }

    private LeafNode Postfix()
    {
        var node = Primary();
        while (At("."))
        {
            Next();
            var name = Peek();
            if (name.Kind != LeafTokenKind.Identifier)
                throw new SyntaxError("expected a field or method name after '.'", name);
            Next();
            if (At("("))
            {
                Next();
                var arguments = new List<LeafNode>();
                while (!At(")"))
                {
                    arguments.Add(LambdaOrExpression());
                    if (At(",")) Next(); else break;
                }
                var close = Expect(")");
                node = new MethodCall(node, name.Text, arguments, name.Start, name.End, node.Start, close.End);
            }
            else
            {
                node = new MemberAccess(node, name.Text, name.Start, name.End, node.Start, name.End);
            }
        }
        return node;
    }

    private LeafNode LambdaOrExpression()
    {
        var token = Peek();
        if (token.Kind == LeafTokenKind.Identifier && Peek(1).Text == "=>")
        {
            Next(); Next();
            var body = Or();
            return new Lambda(token.Text, body, token.Start, body.End);
        }
        return Or();
    }

    private LeafNode Primary()
    {
        var token = Next();
        switch (token.Kind)
        {
            case LeafTokenKind.Number: return new NumberLiteral(token.Text, token.Start, token.End);
            case LeafTokenKind.String: return new StringLiteral(token.Text.Substring(1, token.Text.Length - 2), token.Start, token.End);
            case LeafTokenKind.Keyword when token.Text == "null": return new NullLiteral(token.Start, token.End);
            case LeafTokenKind.Keyword: return new BoolLiteral(token.Text == "true", token.Start, token.End);
            case LeafTokenKind.Parameter: return new ParameterRef(token.Text.Substring(1), token.Start, token.End);
            case LeafTokenKind.Identifier: return new Identifier(token.Text, token.Start, token.End);
            case LeafTokenKind.Punctuation when token.Text == "(":
            {
                var inner = Or();
                var close = Expect(")");
                return inner with { Start = token.Start, End = close.End };
            }
            default:
                throw new SyntaxError($"unexpected '{token.Text}'", token);
        }
    }
}
