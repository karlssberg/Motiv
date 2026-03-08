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
internal class SpecInvocationReplacer(
    string propositionName,
    string defaultModelName,
    ISpecFieldCustomizer fieldCustomizer)
{
    private bool _isMethodStatic;

    private string FieldName => _isMethodStatic ? propositionName : $"_{propositionName.ToCamelCase()}";

    /// <summary>
    ///     Replaces the logical expression in the containing class with a spec field and invocation.
    /// </summary>
    /// <param name="syntaxContext">The syntax context for trivia handling.</param>
    /// <param name="variableSymbols">The variables referenced by the expression.</param>
    /// <param name="logicalExpressionSyntax">The original logical expression.</param>
    /// <param name="root">The syntax root to transform.</param>
    /// <param name="hasInstanceMethods">Whether the expression contains instance method calls.</param>
    /// <param name="groupedExpression">The expression after and-chain grouping.</param>
    /// <param name="modelTypeName">The model type name for the field type.</param>
    /// <returns>The updated syntax root.</returns>
    public SyntaxNode Replace(
        SyntaxContext syntaxContext,
        ImmutableArray<ISymbol> variableSymbols,
        ExpressionSyntax logicalExpressionSyntax,
        SyntaxNode root,
        bool hasInstanceMethods,
        ExpressionSyntax groupedExpression,
        string? modelTypeName = null)
    {
        var method = logicalExpressionSyntax.Ancestors().OfType<MethodDeclarationSyntax>().First();
        _isMethodStatic = method.Modifiers.Any(SyntaxKind.StaticKeyword);
        var containingClass = method.Ancestors().OfType<ClassDeclarationSyntax>().First();
        var statement = logicalExpressionSyntax.Ancestors().OfType<StatementSyntax>().FirstOrDefault();

        var commentTrivia = BuildCommentTrivia(groupedExpression);
        var specInvocation = BuildSpecInvocationExpression(variableSymbols);

        var resultVarName = DeriveResultVarName();

        var field = BuildFieldDeclaration(hasInstanceMethods, modelTypeName);
        var replacementMethod = BuildReplacementMethod(method, statement, resultVarName, specInvocation, commentTrivia);
        ConstructorDeclarationSyntax? constructor = hasInstanceMethods
            ? BuildConstructor(containingClass)
            : null;

        var lineFeed = syntaxContext.LineFeed;
        var eol = lineFeed.ToString();

        var newField = FormatMember(field, eol, lineFeed);
        var newMethod = (MemberDeclarationSyntax)replacementMethod.NormalizeWhitespace(eol: eol);
        var newConstructor = constructor is not null
            ? FormatMember(constructor, eol, lineFeed)
            : null;

        var indent = GetIndentFromOriginalMethod(method);
        if (indent.Length > 0)
        {
            newField = SyntaxIndentHelper.ReindentMember(newField, indent);
            newMethod = SyntaxIndentHelper.ReindentMember(newMethod, indent);
            if (newConstructor is not null)
                newConstructor = SyntaxIndentHelper.ReindentMember(newConstructor, indent);
        }

        newField = newField.WithTrailingTrivia(lineFeed);
        newMethod = newMethod.WithTrailingTrivia(lineFeed);
        if (newConstructor is not null)
            newConstructor = newConstructor.WithTrailingTrivia(lineFeed, lineFeed);

        if (!hasInstanceMethods && variableSymbols.Length == 1)
            newMethod = newMethod.WithLeadingTrivia(method.GetLeadingTrivia());

        return ApplyMemberChanges(root, containingClass, method, newField, newMethod, newConstructor, FieldName);
    }

    private MemberDeclarationSyntax FormatMember(
        MemberDeclarationSyntax member,
        string eol,
        SyntaxTrivia lineFeed) =>
        fieldCustomizer.FormatMember(member.NormalizeWhitespace(eol: eol), lineFeed);

    private FieldDeclarationSyntax BuildFieldDeclaration(bool hasInstanceMethods, string? modelTypeName)
    {
        var fieldType = fieldCustomizer.GetFieldType(propositionName, modelTypeName);
        var declarator = VariableDeclarator(Identifier(FieldName));

        if (!hasInstanceMethods)
        {
            var initializer = fieldCustomizer.GetFieldInitializer(propositionName);
            declarator = declarator.WithInitializer(EqualsValueClause(initializer));
        }

        var modifiers = _isMethodStatic
            ? TokenList(
                Token(SyntaxKind.PrivateKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.ReadOnlyKeyword))
            : TokenList(
                Token(SyntaxKind.PrivateKeyword),
                Token(SyntaxKind.ReadOnlyKeyword));

        return FieldDeclaration(
                VariableDeclaration(fieldType)
                    .WithVariables(SingletonSeparatedList(declarator)))
            .WithModifiers(modifiers);
    }

    private MethodDeclarationSyntax BuildReplacementMethod(
        MethodDeclarationSyntax originalMethod,
        StatementSyntax? statement,
        string resultVarName,
        ExpressionSyntax specInvocation,
        SyntaxTriviaList commentTrivia)
    {
        var evaluateStatement = LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(resultVarName))
                        .WithInitializer(EqualsValueClause(specInvocation)))))
            .WithLeadingTrivia(commentTrivia);

        var assignmentStatement = BuildAssignmentStatement(statement, originalMethod, resultVarName);

        var body = Block(evaluateStatement, assignmentStatement);

        return originalMethod
            .WithAttributeLists(List<AttributeListSyntax>())
            .WithExpressionBody(null)
            .WithSemicolonToken(Token(SyntaxKind.None))
            .WithBody(body);
    }

    private ConstructorDeclarationSyntax BuildConstructor(ClassDeclarationSyntax containingClass)
    {
        var assignment = fieldCustomizer.GetConstructorAssignment(propositionName);

        var assignmentStatement = ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(FieldName),
                assignment));

        return ConstructorDeclaration(containingClass.Identifier)
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithBody(Block(assignmentStatement));
    }

    private InvocationExpressionSyntax BuildSpecInvocationExpression(
        ImmutableArray<ISymbol> variableSymbols)
    {
        var evaluateAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            IdentifierName(FieldName),
            IdentifierName("Evaluate"));

        ArgumentSyntax argument;
        if (variableSymbols.Length == 1)
        {
            argument = Argument(IdentifierName(variableSymbols.First().Name));
        }
        else
        {
            var modelArgs = variableSymbols.Select(s => Argument(IdentifierName(s.Name)));
            var modelCreation = ObjectCreationExpression(
                    QualifiedName(IdentifierName(propositionName), IdentifierName(defaultModelName)))
                .WithArgumentList(ArgumentList(SeparatedList(modelArgs)));
            argument = Argument(modelCreation);
        }

        return InvocationExpression(evaluateAccess)
            .WithArgumentList(ArgumentList(SingletonSeparatedList(argument)));
    }

    private static StatementSyntax BuildAssignmentStatement(
        StatementSyntax? statement,
        MethodDeclarationSyntax method,
        string resultVarName)
    {
        var satisfiedAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            IdentifierName(resultVarName),
            IdentifierName("Satisfied"));

        return statement switch
        {
            ReturnStatementSyntax => ReturnStatement(satisfiedAccess),
            LocalDeclarationStatementSyntax local =>
                LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(local.Declaration.Variables.First().Identifier)
                                .WithInitializer(EqualsValueClause(satisfiedAccess))))),
            ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment } =>
                ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        assignment.Left,
                        satisfiedAccess)),
            null when method.ExpressionBody is not null && method.ReturnType.ToString() == "bool"
                => ReturnStatement(satisfiedAccess),
            _ => LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(Identifier("isSatisfied"))
                            .WithInitializer(EqualsValueClause(satisfiedAccess)))))
        };
    }

    private static SyntaxTriviaList BuildCommentTrivia(ExpressionSyntax expression)
    {
        var normalized = expression.NormalizeWhitespace().ToFullString();
        var parts = normalized.Split([" || "], 2, StringSplitOptions.None);

        var trivia = new List<SyntaxTrivia>();
        if (parts.Length <= 1)
        {
            trivia.Add(Comment($"// {normalized}"));
            trivia.Add(EndOfLine("\n"));
        }
        else
        {
            trivia.Add(Comment($"// {parts[0]} ||"));
            trivia.Add(EndOfLine("\n"));
            trivia.Add(Comment($"//     {parts[1]}"));
            trivia.Add(EndOfLine("\n"));
        }

        return TriviaList(trivia);
    }

    private string DeriveResultVarName()
    {
        var baseName = propositionName.EndsWith("Proposition")
            ? propositionName.Substring(0, propositionName.Length - "Proposition".Length)
            : propositionName;
        return $"{baseName.ToCamelCase()}Result";
    }

    private static string GetIndentFromOriginalMethod(MethodDeclarationSyntax method) =>
        method
            .GetLeadingTrivia()
            .LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia))
            .ToString();

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

        SyntaxTrivia[] openBraceTrivia = classLeadingWhitespace.RawKind == 0
            ? [EndOfLine("\n")]
            : [EndOfLine("\n"), classLeadingWhitespace];

        return classDeclaration
            .WithParameterList(null)
            .WithOpenBraceToken(classDeclaration.OpenBraceToken.WithLeadingTrivia(openBraceTrivia));
    }
}
