using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.CodeFix.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix;

/// <summary>
///     Replaces a logical expression in a method with a spec invocation,
///     adding a field declaration (and optionally a constructor) to the containing class.
/// </summary>
internal class SpecInvocationReplacer(string propositionName, string defaultModelName)
{
    /// <summary>
    ///     Replaces the logical expression in the containing class with a spec field and invocation.
    /// </summary>
    /// <param name="syntaxContext">The syntax context for trivia handling.</param>
    /// <param name="variableSymbols">The variables referenced by the expression.</param>
    /// <param name="logicalExpressionSyntax">The original logical expression.</param>
    /// <param name="root">The syntax root to transform.</param>
    /// <param name="hasInstanceMethods">Whether the expression contains instance method calls.</param>
    /// <param name="groupedExpression">The expression after and-chain grouping.</param>
    /// <returns>The updated syntax root.</returns>
    public SyntaxNode Replace(
        SyntaxContext syntaxContext,
        ImmutableArray<ISymbol> variableSymbols,
        ExpressionSyntax logicalExpressionSyntax,
        SyntaxNode root,
        bool hasInstanceMethods,
        ExpressionSyntax groupedExpression)
    {
        var method = logicalExpressionSyntax.Ancestors().OfType<MethodDeclarationSyntax>().First();
        var containingClass = method.Ancestors().OfType<ClassDeclarationSyntax>().First();
        var statement = logicalExpressionSyntax.Ancestors().OfType<StatementSyntax>().FirstOrDefault();

        var fieldName = $"_{propositionName.ToCamelCase()}";
        var originalExprText = SyntaxIndentHelper.FormatAsComment(groupedExpression);

        var specInvocation = BuildSpecInvocation(variableSymbols, fieldName);
        var fieldDeclaration = BuildFieldDeclaration(hasInstanceMethods, fieldName);

        var useMethodDerivedName = variableSymbols.Length == 1
            || (hasInstanceMethods && statement is null && method.ExpressionBody is not null);
        var resultVarName = useMethodDerivedName ? DeriveResultVarName(method) : "result";
        var assignmentLine = DeriveAssignmentLine(statement, method, resultVarName);

        var newMethodSource = BuildTempClassSource(
            hasInstanceMethods, fieldDeclaration, containingClass, method,
            originalExprText, resultVarName, specInvocation, assignmentLine);

        var tempUnit = ParseCompilationUnit(newMethodSource);
        var tempClass = tempUnit.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        MemberDeclarationSyntax newField = tempClass.Members.OfType<FieldDeclarationSyntax>().First()
            .WithTrailingTrivia(syntaxContext.LineFeed);
        MemberDeclarationSyntax newMethod = tempClass.Members.OfType<MethodDeclarationSyntax>().Last();
        MemberDeclarationSyntax? newConstructor = hasInstanceMethods
            ? tempClass.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault()
            : null;

        var extraIndent = GetExtraIndentFromOriginalMethod(method);
        if (extraIndent.Length > 0)
        {
            newField = SyntaxIndentHelper.ReindentMember(newField, extraIndent);
            newMethod = SyntaxIndentHelper.ReindentMember(newMethod, extraIndent);
            if (newConstructor is not null)
                newConstructor = SyntaxIndentHelper.ReindentMember(newConstructor, extraIndent);
        }

        if (!hasInstanceMethods && variableSymbols.Length == 1)
            newMethod = newMethod.WithLeadingTrivia(method.GetLeadingTrivia());

        return ApplyMemberChanges(root, containingClass, method, newField, newMethod, newConstructor, fieldName);
    }

    private string BuildSpecInvocation(ImmutableArray<ISymbol> variableSymbols, string fieldName)
    {
        if (variableSymbols.Length == 1)
            return $"{fieldName}.IsSatisfiedBy({variableSymbols.First().Name})";

        var modelArgs = string.Join(", ", variableSymbols.Select(s => s.Name));
        return $"{fieldName}.IsSatisfiedBy(new {propositionName}.{defaultModelName}({modelArgs}))";
    }

    private string BuildFieldDeclaration(bool hasInstanceMethods, string fieldName) =>
        hasInstanceMethods
            ? $"private readonly {propositionName} {fieldName};"
            : $"private readonly {propositionName} {fieldName} = new();";

    private string BuildTempClassSource(
        bool hasInstanceMethods,
        string fieldDeclaration,
        ClassDeclarationSyntax containingClass,
        MethodDeclarationSyntax method,
        string originalExprText,
        string resultVarName,
        string specInvocation,
        string assignmentLine) =>
        hasInstanceMethods
            ? $$"""
                class Temp
                {
                    {{fieldDeclaration}}
                    public {{containingClass.Identifier}}()
                    {
                        {{$"_{propositionName.ToCamelCase()}"}} = new {{propositionName}}(this);
                    }

                    public {{method.ReturnType}} {{method.Identifier}}{{method.ParameterList}}
                    {
                        // {{originalExprText}}
                        var {{resultVarName}} = {{specInvocation}};
                        {{assignmentLine}}
                    }
                }
                """
            : $$"""
                class Temp
                {
                    {{fieldDeclaration}}
                    public {{method.ReturnType}} {{method.Identifier}}{{method.ParameterList}}
                    {
                        // {{originalExprText}}
                        var {{resultVarName}} = {{specInvocation}};
                        {{assignmentLine}}
                    }
                }
                """;

    private static string DeriveResultVarName(MethodDeclarationSyntax method) =>
        $"{method.Identifier.ValueText.ToCamelCase()}Result";

    private static string DeriveAssignmentLine(
        StatementSyntax? statement,
        MethodDeclarationSyntax method,
        string resultVarName) =>
        statement switch
        {
            ReturnStatementSyntax => $"return {resultVarName}.Satisfied;",
            LocalDeclarationStatementSyntax local =>
                $"var {local.Declaration.Variables.First().Identifier.Text} = {resultVarName}.Satisfied;",
            ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment } =>
                $"{assignment.Left} = {resultVarName}.Satisfied;",
            null when method.ExpressionBody is not null && method.ReturnType.ToString() == "bool"
                => $"return {resultVarName}.Satisfied;",
            _ => $"var isSatisfied = {resultVarName}.Satisfied;"
        };

    private static string GetExtraIndentFromOriginalMethod(MethodDeclarationSyntax method)
    {
        const string templateBaseIndent = "    ";
        var originalIndent = method
            .GetLeadingTrivia()
            .LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia))
            .ToString();

        return originalIndent.Length > templateBaseIndent.Length
            ? originalIndent.Substring(templateBaseIndent.Length)
            : "";
    }

    private static SyntaxNode ApplyMemberChanges(
        SyntaxNode root,
        ClassDeclarationSyntax containingClass,
        MethodDeclarationSyntax method,
        MemberDeclarationSyntax newField,
        MemberDeclarationSyntax newMethod,
        MemberDeclarationSyntax? newConstructor,
        string fieldName)
    {
        var fieldAdded = containingClass.Members.OfType<FieldDeclarationSyntax>()
            .Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == fieldName));

        var existingMembers = containingClass.Members
            .Select(m => m == method ? newMethod : m).ToList();

        if (!fieldAdded)
            InsertAfterLastField(existingMembers, newField);
        if (newConstructor is not null)
            InsertAfterLastField(existingMembers, newConstructor);

        var newClass = containingClass.WithMembers(List(existingMembers));
        newClass = RemovePrimaryConstructorIfNeeded(newClass, newConstructor);

        return root.ReplaceNode(containingClass, newClass);
    }

    private static void InsertAfterLastField(List<MemberDeclarationSyntax> members, MemberDeclarationSyntax member)
    {
        var lastFieldIndex = -1;
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i] is FieldDeclarationSyntax)
                lastFieldIndex = i;
        }

        members.Insert(lastFieldIndex + 1, member);
    }

    private static ClassDeclarationSyntax RemovePrimaryConstructorIfNeeded(
        ClassDeclarationSyntax classDeclaration,
        MemberDeclarationSyntax? newConstructor)
    {
        if (newConstructor is null || classDeclaration.ParameterList is null)
            return classDeclaration;

        var classLeadingWhitespace = classDeclaration
            .GetLeadingTrivia()
            .LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia));

        var openBraceTrivia = classLeadingWhitespace.RawKind != 0
            ? new[] { EndOfLine("\n"), classLeadingWhitespace }
            : new[] { EndOfLine("\n") };

        return classDeclaration
            .WithParameterList(null)
            .WithOpenBraceToken(classDeclaration.OpenBraceToken.WithLeadingTrivia(openBraceTrivia));
    }
}
