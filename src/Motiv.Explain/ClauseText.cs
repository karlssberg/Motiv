using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Motiv.Explain;

/// <summary>
///     A clause is one operand, or a run of consecutive operands of one <c>&amp;&amp;</c>/<c>||</c> chain that
///     had to stay together. Its text is the source between them, operators included.
/// </summary>
internal static class ClauseText
{
    public static TextSpan Span(IReadOnlyList<ExpressionSyntax> clause) =>
        TextSpan.FromBounds(clause[0].SpanStart, clause[clause.Count - 1].Span.End);

    public static string Of(IReadOnlyList<ExpressionSyntax> clause) =>
        clause[0].SyntaxTree.GetText().ToString(Span(clause));
}
