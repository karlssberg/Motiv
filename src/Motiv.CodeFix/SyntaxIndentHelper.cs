using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix;

/// <summary>
///     Provides shared utilities for reindenting syntax members.
/// </summary>
internal static class SyntaxIndentHelper
{
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
}
