namespace Motiv.Serialization.Expressions;

internal static class LeafTokenizer
{
    private static readonly HashSet<string> Keywords = ["null", "true", "false"];
    private static readonly string[] TwoCharOperators = ["=>", "==", "!=", "<=", ">=", "&&", "||"];

    // ASCII only, to match the TypeScript tokenizer's [A-Za-z_]/[A-Za-z0-9_]/[0-9] regexes exactly
    // — char.IsLetter/IsLetterOrDigit/IsDigit are Unicode-aware and would accept identifiers and
    // digits the TS side rejects as syntax errors (e.g. 'å' is a Unicode letter but not ASCII).
    private static bool IsAsciiLetter(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';
    private static bool IsAsciiLetterOrDigit(char c) => IsAsciiLetter(c) || IsAsciiDigit(c);

    public static IReadOnlyList<LeafToken> Tokenize(string text, List<LeafProblem> problems)
    {
        var tokens = new List<LeafToken>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            var start = i;
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (IsAsciiLetter(c) || c == '_')
            {
                while (i < text.Length && (IsAsciiLetterOrDigit(text[i]) || text[i] == '_')) i++;
                var word = text.Substring(start, i - start);
                tokens.Add(new LeafToken(Keywords.Contains(word) ? LeafTokenKind.Keyword : LeafTokenKind.Identifier, word, start, i));
                continue;
            }

            if (c == '@')
            {
                i++;
                while (i < text.Length && (IsAsciiLetterOrDigit(text[i]) || text[i] == '_')) i++;
                if (i == start + 1)
                    problems.Add(new LeafProblem(RuleErrorCode.InvalidExpression, "expected a parameter name after '@'", start, i));
                tokens.Add(new LeafToken(LeafTokenKind.Parameter, text.Substring(start, i - start), start, i));
                continue;
            }

            if (IsAsciiDigit(c))
            {
                while (i < text.Length && IsAsciiDigit(text[i])) i++;
                if (i + 1 < text.Length && text[i] == '.' && IsAsciiDigit(text[i + 1]))
                {
                    i++;
                    while (i < text.Length && IsAsciiDigit(text[i])) i++;
                }
                tokens.Add(new LeafToken(LeafTokenKind.Number, text.Substring(start, i - start), start, i));
                continue;
            }

            if (c == '"')
            {
                i++;
                while (i < text.Length && text[i] != '"') i++;
                if (i >= text.Length)
                {
                    problems.Add(new LeafProblem(RuleErrorCode.InvalidExpression, "unterminated string", start, text.Length));
                    tokens.Add(new LeafToken(LeafTokenKind.String, text.Substring(start), start, text.Length));
                    continue;
                }
                i++;
                tokens.Add(new LeafToken(LeafTokenKind.String, text.Substring(start, i - start), start, i));
                continue;
            }

            var two = i + 1 < text.Length ? text.Substring(i, 2) : null;
            if (two is not null && Array.IndexOf(TwoCharOperators, two) >= 0)
            {
                i += 2;
                tokens.Add(new LeafToken(LeafTokenKind.Operator, two, start, i));
                continue;
            }

            if ("<>!+-*/".IndexOf(c) >= 0)
            {
                i++;
                tokens.Add(new LeafToken(LeafTokenKind.Operator, c.ToString(), start, i));
                continue;
            }

            if ("().,".IndexOf(c) >= 0)
            {
                i++;
                tokens.Add(new LeafToken(LeafTokenKind.Punctuation, c.ToString(), start, i));
                continue;
            }

            problems.Add(new LeafProblem(RuleErrorCode.InvalidExpression, $"unexpected character '{c}'", start, start + 1));
            i++;
        }

        tokens.Add(new LeafToken(LeafTokenKind.End, string.Empty, text.Length, text.Length));
        return tokens;
    }
}
