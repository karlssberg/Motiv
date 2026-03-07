using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Motiv.CodeFix.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix;

/// <summary>
///     Places generated spec classes into the correct location in the document —
///     inside a namespace, at the compilation unit level, or before orphan braces.
/// </summary>
internal static class SpecClassPlacer
{
    /// <summary>
    ///     Adds spec classes near the containing class, inside the appropriate namespace or compilation unit.
    /// </summary>
    /// <param name="syntaxContext">The syntax context for trivia handling.</param>
    /// <param name="newRoot">The current syntax root.</param>
    /// <param name="baseNamespace">The namespace declaration, if any.</param>
    /// <param name="specClasses">The spec class declarations to add.</param>
    /// <returns>The updated syntax root.</returns>
    public static SyntaxNode AddNearContainingClass(
        SyntaxContext syntaxContext,
        SyntaxNode newRoot,
        BaseNamespaceDeclarationSyntax? baseNamespace,
        MemberDeclarationSyntax[] specClasses)
    {
        if (specClasses.Length == 0) return newRoot;

        return baseNamespace is not null
            ? AddToNamespace(syntaxContext, newRoot, baseNamespace, specClasses)
            : AddToCompilationUnit(syntaxContext, newRoot, specClasses);
    }

    /// <summary>
    ///     Adds <c>using Motiv;</c> to the compilation unit if not already present.
    /// </summary>
    /// <param name="newRoot">The syntax root to update.</param>
    /// <returns>The updated syntax root with using statements.</returns>
    public static SyntaxNode AddUsingStatementsIfNeeded(SyntaxNode newRoot)
    {
        var compilationUnit = (CompilationUnitSyntax)newRoot;

        if (compilationUnit.Usings.Any(u => u.Name?.ToString() == nameof(Motiv)))
            return newRoot;

        var motivUsing = UsingDirective(IdentifierName(nameof(Motiv)))
            .NormalizeWhitespace()
            .WithTrailingTrivia(EndOfLine("\n"), EndOfLine("\n"));

        return compilationUnit.AddUsings(motivUsing);
    }

    /// <summary>
    ///     Moves spec classes that appear after an orphan closing brace to before it.
    ///     Only needed for file-scoped namespaces where the orphan <c>}</c> from the original
    ///     block namespace becomes trailing trivia of a member.
    /// </summary>
    /// <param name="doc">The document to fix.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The corrected document.</returns>
    public static async Task<Document> MoveSpecClassesBeforeOrphanBrace(
        Document doc,
        CancellationToken cancellationToken)
    {
        var root = await doc.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var fileNs = root?.DescendantNodes()
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault();
        if (fileNs is null) return doc;

        var (memberWithBrace, memberIndex) = FindMemberWithOrphanBrace(fileNs);

        if (memberWithBrace is null || memberIndex >= fileNs.Members.Count - 1)
            return doc;

        var text = await doc.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var textStr = text.ToString();

        var trailingTrivia = memberWithBrace.GetTrailingTrivia();
        var braceTrivia = trailingTrivia.FirstOrDefault(t => t.ToString().Contains("}"));
        if (braceTrivia == default) return doc;

        var braceStart = braceTrivia.SpanStart;
        var contentToMove = ExtractContentAfterBrace(textStr, braceStart);
        if (string.IsNullOrWhiteSpace(contentToMove)) return doc;

        var lineEnding = textStr.Contains("\r\n") ? "\r\n" : "\n";
        var memberIndent = GetMemberIndent(memberWithBrace);
        var reindentedText = ReindentContent(contentToMove, memberIndent, lineEnding);
        if (reindentedText is null) return doc;

        var insertPos = FindLineStart(textStr, braceStart);
        var deleteStart = braceStart + 1;

        var changes = new[]
        {
            new TextChange(
                new TextSpan(insertPos, 0),
                reindentedText + lineEnding),
            new TextChange(
                new TextSpan(deleteStart, textStr.Length - deleteStart),
                "")
        };

        return doc.WithText(text.WithChanges(changes));
    }

    private static (MemberDeclarationSyntax? Member, int Index) FindMemberWithOrphanBrace(
        FileScopedNamespaceDeclarationSyntax fileNs)
    {
        for (var i = 0; i < fileNs.Members.Count; i++)
        {
            if (fileNs.Members[i].GetTrailingTrivia().ToString().Contains("}"))
                return (fileNs.Members[i], i);
        }

        return (null, -1);
    }

    private static string ExtractContentAfterBrace(string textStr, int braceStart)
    {
        var braceLineEnd = textStr.IndexOf('\n', braceStart);
        if (braceLineEnd < 0) braceLineEnd = textStr.Length;
        else braceLineEnd++;

        var content = textStr.Substring(braceLineEnd);
        return content.TrimEnd();
    }

    private static string GetMemberIndent(MemberDeclarationSyntax member) =>
        member.GetLeadingTrivia()
            .LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia))
            .ToString();

    private static string? ReindentContent(string contentToMove, string memberIndent, string lineEnding)
    {
        var contentLines = contentToMove.Split('\n');
        var reindented = new List<string>();
        string? contentBaseIndent = null;

        foreach (var line in contentLines)
        {
            var raw = line.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(raw))
            {
                reindented.Add("");
                continue;
            }

            if (contentBaseIndent is null)
            {
                var trimmedLeft = raw.TrimStart();
                contentBaseIndent = raw.Substring(0, raw.Length - trimmedLeft.Length);
            }

            var stripped = raw.StartsWith(contentBaseIndent)
                ? raw.Substring(contentBaseIndent.Length)
                : raw;
            reindented.Add(memberIndent + stripped);
        }

        while (reindented.Count > 0 && string.IsNullOrWhiteSpace(reindented[0]))
            reindented.RemoveAt(0);
        while (reindented.Count > 0 && string.IsNullOrWhiteSpace(reindented[reindented.Count - 1]))
            reindented.RemoveAt(reindented.Count - 1);

        return reindented.Count == 0 ? null : string.Join(lineEnding, reindented);
    }

    private static int FindLineStart(string textStr, int position)
    {
        var insertPos = position;
        while (insertPos > 0 && textStr[insertPos - 1] != '\n')
            insertPos--;
        return insertPos;
    }

    private static SyntaxNode AddToCompilationUnit(
        SyntaxContext syntaxContext,
        SyntaxNode newRoot,
        params MemberDeclarationSyntax[] customSpecClassDeclaration)
    {
        var compilationUnit = (CompilationUnitSyntax)newRoot;
        var membersWithTrivia = customSpecClassDeclaration.Select(member =>
            member.WithLeadingTrivia(syntaxContext.LineFeed, syntaxContext.LineFeed));

        return compilationUnit.AddMembers(membersWithTrivia.ToArray());
    }

    private static SyntaxNode AddToNamespace(
        SyntaxContext syntaxContext,
        SyntaxNode newRoot,
        BaseNamespaceDeclarationSyntax namespaceDeclaration,
        params MemberDeclarationSyntax[] customSpecClassDeclaration)
    {
        var isBlockNamespace = namespaceDeclaration is NamespaceDeclarationSyntax;
        var indent = isBlockNamespace ? syntaxContext.BaselineIndent.ToString() : "";
        var lastMember = namespaceDeclaration.Members.LastOrDefault();
        var lastMemberHasTrailingNewline = lastMember?.GetTrailingTrivia()
            .Any(t => t.IsKind(SyntaxKind.EndOfLineTrivia)) ?? false;

        var membersWithTrivia = customSpecClassDeclaration.Select(member =>
        {
            member = isBlockNamespace && lastMemberHasTrailingNewline
                ? member.WithLeadingTrivia(syntaxContext.LineFeed)
                : member.WithLeadingTrivia(syntaxContext.LineFeed, syntaxContext.LineFeed);
            if (indent.Length > 0)
                member = SyntaxIndentHelper.ReindentMember(member, indent);
            return member;
        });

        var updatedNamespace = namespaceDeclaration
            .AddMembers(membersWithTrivia.ToArray());

        if (isBlockNamespace && updatedNamespace is NamespaceDeclarationSyntax blockNs)
        {
            updatedNamespace = blockNs.WithCloseBraceToken(
                blockNs.CloseBraceToken.WithLeadingTrivia(syntaxContext.LineFeed));
        }

        return newRoot.ReplaceNode(namespaceDeclaration, updatedNamespace);
    }
}
