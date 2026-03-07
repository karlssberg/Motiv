using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix;

/// <summary>
///     Provides shared utilities for reindenting syntax members and formatting expressions as comments.
/// </summary>
internal static class SyntaxIndentHelper
{
    // Matches the method body indent (8 spaces) inside the raw string template in SpecInvocationReplacer
    private const string TemplateBodyIndent = "        ";
    private const string CommentContinuationPrefix = TemplateBodyIndent + "//     ";

    /// <summary>
    ///     Reindents a member declaration by prepending extra whitespace to each non-empty line.
    /// </summary>
    /// <param name="member">The member to reindent.</param>
    /// <param name="extraIndent">The additional indentation to prepend.</param>
    /// <returns>The reindented member, or the original if parsing fails.</returns>
    public static MemberDeclarationSyntax ReindentMember(MemberDeclarationSyntax member, string extraIndent)
    {
        var text = member.ToFullString();
        var lines = text.Split('\n');
        var reindented = string.Join("\n", lines.Select(line =>
        {
            var trimmedEnd = line.TrimEnd('\r');
            return string.IsNullOrWhiteSpace(trimmedEnd)
                ? trimmedEnd
                : $"{extraIndent}{trimmedEnd}";
        }));
        return ParseMemberDeclaration(reindented) ?? member;
    }

    /// <summary>
    ///     Formats an expression as a comment string, splitting at <c>||</c> boundaries for readability.
    /// </summary>
    /// <param name="expression">The expression to format.</param>
    /// <returns>A normalized, optionally multiline comment string.</returns>
    public static string FormatAsComment(ExpressionSyntax expression)
    {
        var normalized = expression.NormalizeWhitespace().ToFullString();

        // Split at " || " to create multiline comment at || boundaries
        var parts = normalized.Split(new[] { " || " }, 2, StringSplitOptions.None);
        if (parts.Length <= 1) return normalized;

        // ReindentMember will add any extra namespace indent later
        return $"{parts[0]} ||\n{CommentContinuationPrefix}{parts[1]}";
    }
}
