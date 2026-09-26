using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.CodeFix.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix;

/// <summary>
///     Replaces a logical expression with a spec evaluation, adding a field declaration (and optionally a
///     constructor) to the containing type. Only the expression is rewritten — or, when it is the whole
///     value of a statement, that one statement — so everything around it is left as it was.
/// </summary>
internal class SpecInvocationReplacer(
    string propositionName,
    string defaultModelName,
    ISpecFieldCustomizer fieldCustomizer)
{
    // A spec is an immutable decision tree, so it is built once per type — unless it captures `this`.
    private bool _isFieldStatic;

    // The spec class's name with any type arguments; a generic spec holds its own instance instead of a field
    private string _specTypeName = string.Empty;
    private bool _isGeneric;

    private string FieldName => _isFieldStatic ? propositionName : $"_{propositionName.ToCamelCase()}";

    private ExpressionSyntax SpecAccess =>
        _isGeneric
            ? ParseExpression($"{_specTypeName}.{SpecTypeParameters.InstanceFieldName}")
            : IdentifierName(FieldName);

    private string ResultVariableName
    {
        get
        {
            var baseName = propositionName.EndsWith("Proposition")
                ? propositionName.Substring(0, propositionName.Length - "Proposition".Length)
                : propositionName;
            return $"{baseName.ToCamelCase()}Result";
        }
    }

    /// <summary>
    ///     Replaces the logical expression in the containing type with a spec field and invocation.
    /// </summary>
    /// <param name="syntaxContext">The syntax context for trivia handling.</param>
    /// <param name="variableSymbols">The variables referenced by the expression.</param>
    /// <param name="logicalExpressionSyntax">The original logical expression.</param>
    /// <param name="root">The syntax root to transform.</param>
    /// <param name="hasInstanceMethods">Whether the expression contains instance method calls.</param>
    /// <param name="groupedExpression">The expression after and-chain grouping.</param>
    /// <param name="specTypeName">The spec class's name with any type arguments.</param>
    /// <param name="modelTypeName">The model type name for the field type.</param>
    /// <returns>The updated syntax root.</returns>
    public SyntaxNode Replace(
        SyntaxContext syntaxContext,
        ImmutableArray<ISymbol> variableSymbols,
        ExpressionSyntax logicalExpressionSyntax,
        SyntaxNode root,
        bool hasInstanceMethods,
        ExpressionSyntax groupedExpression,
        string specTypeName,
        string? modelTypeName = null)
    {
        _isFieldStatic = !hasInstanceMethods;
        _specTypeName = specTypeName;
        _isGeneric = specTypeName != propositionName;
        var containingType = syntaxContext.ContainingType
            ?? throw new InvalidOperationException("The expression is not inside a type declaration.");
        var lineFeed = syntaxContext.LineFeed;

        var expression = OutermostParentheses(logicalExpressionSyntax);
        var model = BuildModelArgument(variableSymbols);
        var comment = new EvaluationComment(groupedExpression, lineFeed);

        var newType = ReplaceExpression(containingType, expression, model, comment);
        if (_isGeneric)
            return root.ReplaceNode(containingType, newType);

        var containingMember = ContainingMember(expression, containingType);
        var indent = GetIndent(containingMember);
        var field = FormatMember(BuildFieldDeclaration(modelTypeName), indent, lineFeed);
        var constructor = hasInstanceMethods
            ? FormatMember(BuildConstructor(containingType), indent, lineFeed)
            : null;

        var containingMemberIndex = containingType.Members.IndexOf(containingMember);
        return root.ReplaceNode(containingType, AddMembers(newType, containingMemberIndex, field, constructor, lineFeed));
    }

    /// <summary>
    ///     Rewrites the smallest node that can hold the evaluation: the statement whose whole value is the
    ///     expression, else a <c>bool</c> expression-bodied method, else the expression itself.
    /// </summary>
    private TypeDeclarationSyntax ReplaceExpression(
        TypeDeclarationSyntax containingType,
        ExpressionSyntax expression,
        ArgumentSyntax model,
        EvaluationComment comment)
    {
        if (FindHostStatement(expression) is { } statement)
            return containingType.ReplaceNode(statement, BuildEvaluatedStatements(statement, expression, model, comment));

        if (FindBoolExpressionBodiedMethod(expression) is { } method)
            return containingType.ReplaceNode(method, BuildBlockBodiedMethod(method, model, comment));

        return containingType.ReplaceNode(expression, BuildSatisfiedCheck(model).WithTriviaFrom(expression));
    }

    private static ExpressionSyntax OutermostParentheses(ExpressionSyntax expression)
    {
        while (expression.Parent is ParenthesizedExpressionSyntax parenthesized)
            expression = parenthesized;
        return expression;
    }

    /// <summary>
    ///     The statement whose whole value is <paramref name="expression" />, when it sits in a block and so can be
    ///     preceded by the statement that evaluates the spec.
    /// </summary>
    private static StatementSyntax? FindHostStatement(ExpressionSyntax expression)
    {
        StatementSyntax? statement = expression.Parent switch
        {
            ReturnStatementSyntax returnStatement => returnStatement,
            EqualsValueClauseSyntax
            {
                Parent: VariableDeclaratorSyntax
                {
                    Parent: VariableDeclarationSyntax
                    {
                        Variables.Count: 1,
                        Parent: LocalDeclarationStatementSyntax local
                    }
                }
            } when !local.IsConst && local.UsingKeyword.IsKind(SyntaxKind.None) => local,
            AssignmentExpressionSyntax { Parent: ExpressionStatementSyntax assignmentStatement } assignment
                when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) => assignmentStatement,
            _ => null
        };

        return statement?.Parent is BlockSyntax or SwitchSectionSyntax ? statement : null;
    }

    private static MethodDeclarationSyntax? FindBoolExpressionBodiedMethod(ExpressionSyntax expression) =>
        expression.Parent is ArrowExpressionClauseSyntax { Parent: MethodDeclarationSyntax method }
        && method.ReturnType is PredefinedTypeSyntax returnType
        && returnType.Keyword.IsKind(SyntaxKind.BoolKeyword)
            ? method
            : null;

    /// <summary>
    ///     <c>var result = Field.Evaluate(model);</c> followed by <paramref name="statement" /> reading
    ///     <c>result.Satisfied</c> — the result stays in scope for whoever wants to know why.
    /// </summary>
    private IEnumerable<StatementSyntax> BuildEvaluatedStatements(
        StatementSyntax statement,
        ExpressionSyntax expression,
        ArgumentSyntax model,
        EvaluationComment comment)
    {
        var indent = GetIndent(statement);

        yield return BuildEvaluateStatement(model)
            .WithLeadingTrivia(statement.GetLeadingTrivia().AddRange(comment.Lines(indent)).Add(indent))
            .WithTrailingTrivia(comment.LineFeed);

        yield return statement
            .ReplaceNode(expression, ResultSatisfied().WithTriviaFrom(expression))
            .WithLeadingTrivia(indent);
    }

    private MethodDeclarationSyntax BuildBlockBodiedMethod(
        MethodDeclarationSyntax method,
        ArgumentSyntax model,
        EvaluationComment comment)
    {
        var lineFeed = comment.LineFeed;
        var indent = GetIndent(method);
        var bodyIndent = Whitespace(indent + "    ");

        var evaluateStatement = BuildEvaluateStatement(model)
            .WithLeadingTrivia(TriviaList(bodyIndent).AddRange(comment.Lines(bodyIndent)).Add(bodyIndent))
            .WithTrailingTrivia(lineFeed);
        var returnStatement = ReturnStatement(ResultSatisfied())
            .NormalizeWhitespace()
            .WithLeadingTrivia(bodyIndent)
            .WithTrailingTrivia(lineFeed);

        var body = Block(evaluateStatement, returnStatement)
            .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken).WithLeadingTrivia(indent).WithTrailingTrivia(lineFeed))
            .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken)
                .WithLeadingTrivia(indent)
                .WithTrailingTrivia(method.SemicolonToken.TrailingTrivia));

        var tokenBeforeArrow = method.ExpressionBody!.ArrowToken.GetPreviousToken();
        return method
            .ReplaceToken(tokenBeforeArrow, tokenBeforeArrow.WithTrailingTrivia(lineFeed))
            .WithExpressionBody(null)
            .WithSemicolonToken(Token(SyntaxKind.None))
            .WithBody(body);
    }

    private LocalDeclarationStatementSyntax BuildEvaluateStatement(ArgumentSyntax model) =>
        LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(Identifier(ResultVariableName))
                            .WithInitializer(EqualsValueClause(
                                InvocationExpression(
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SpecAccess, IdentifierName("Evaluate")))
                                    .WithArgumentList(ArgumentList(SingletonSeparatedList(model))))))))
            .NormalizeWhitespace();

    private MemberAccessExpressionSyntax ResultSatisfied() =>
        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(ResultVariableName), IdentifierName("Satisfied"));

    private ExpressionSyntax BuildSatisfiedCheck(ArgumentSyntax model) =>
        fieldCustomizer.GetSatisfiedCheck(SpecAccess, model).NormalizeWhitespace();

    private ArgumentSyntax BuildModelArgument(ImmutableArray<ISymbol> variableSymbols)
    {
        if (variableSymbols.Length == 1)
            return Argument(IdentifierName(variableSymbols.First().Name));

        var modelArgs = variableSymbols.Select(s => Argument(IdentifierName(s.Name)));
        return Argument(
            ObjectCreationExpression(ParseTypeName($"{_specTypeName}.{defaultModelName}"))
                .WithArgumentList(ArgumentList(SeparatedList(modelArgs))));
    }

    private FieldDeclarationSyntax BuildFieldDeclaration(string? modelTypeName)
    {
        var fieldType = fieldCustomizer.GetFieldType(propositionName, modelTypeName);
        var declarator = VariableDeclarator(Identifier(FieldName));

        if (_isFieldStatic)
        {
            var initializer = fieldCustomizer.GetFieldInitializer(propositionName);
            declarator = declarator.WithInitializer(EqualsValueClause(initializer));
        }

        var modifiers = _isFieldStatic
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

    private ConstructorDeclarationSyntax BuildConstructor(TypeDeclarationSyntax containingType)
    {
        var assignment = fieldCustomizer.GetConstructorAssignment(propositionName);

        var assignmentStatement = ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(FieldName),
                assignment));

        return ConstructorDeclaration(containingType.Identifier.WithoutTrivia())
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithBody(Block(assignmentStatement));
    }

    private MemberDeclarationSyntax FormatMember(MemberDeclarationSyntax member, SyntaxTrivia indent, SyntaxTrivia lineFeed)
    {
        var formatted = fieldCustomizer.FormatMember(member.NormalizeWhitespace(eol: lineFeed.ToString()), lineFeed);
        if (indent.Span.Length > 0)
            formatted = SyntaxIndentHelper.ReindentMember(formatted, indent.ToString());
        return formatted.WithTrailingTrivia(lineFeed);
    }

    private static MemberDeclarationSyntax ContainingMember(ExpressionSyntax expression, TypeDeclarationSyntax containingType) =>
        expression.Ancestors().OfType<MemberDeclarationSyntax>().First(member => member.Parent == containingType);

    private static SyntaxTrivia GetIndent(SyntaxNode node)
    {
        var whitespace = node.GetLeadingTrivia().LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia));
        return whitespace.IsKind(SyntaxKind.WhitespaceTrivia) ? whitespace : Whitespace("");
    }

    private TypeDeclarationSyntax AddMembers(
        TypeDeclarationSyntax containingType,
        int containingMemberIndex,
        MemberDeclarationSyntax field,
        MemberDeclarationSyntax? constructor,
        SyntaxTrivia lineFeed)
    {
        var fieldAdded = containingType.Members.OfType<FieldDeclarationSyntax>()
            .Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == FieldName));

        var members = containingType.Members.ToList();

        // Static initializers run in textual order, so the spec must be declared before the member reading it
        if (!fieldAdded)
            InsertMember(members, Math.Min(AfterLastField(members), containingMemberIndex), field, lineFeed);
        if (constructor is not null)
            InsertMember(members, AfterLastField(members), constructor, lineFeed);

        var newType = containingType.WithMembers(List(members));
        return constructor is null ? newType : RemovePrimaryConstructor(newType, lineFeed);
    }

    private static int AfterLastField(List<MemberDeclarationSyntax> members) =>
        members.FindLastIndex(m => m is FieldDeclarationSyntax) + 1;

    /// <summary>
    ///     Inserts <paramref name="member" /> at <paramref name="index" />, separated by a blank line from a
    ///     following member that is not a field and has none of its own. A blank line an earlier conversion left
    ///     below the member before it moves down, so the fields stay together.
    /// </summary>
    private static void InsertMember(List<MemberDeclarationSyntax> members, int index, MemberDeclarationSyntax member, SyntaxTrivia lineFeed)
    {
        var next = index < members.Count ? members[index] : null;

        var needsBlankLine = next is not null and not FieldDeclarationSyntax
            && !next.GetLeadingTrivia().FirstOrDefault().IsKind(SyntaxKind.EndOfLineTrivia);

        if (index > 0 && EndsWithBlankLine(members[index - 1]))
        {
            members[index - 1] = members[index - 1].WithTrailingTrivia(lineFeed);
            needsBlankLine = true;
        }

        members.Insert(index, needsBlankLine ? member.WithTrailingTrivia(lineFeed, lineFeed) : member);
    }

    private static bool EndsWithBlankLine(MemberDeclarationSyntax member) =>
        member.GetTrailingTrivia() is { Count: >= 2 } trivia
        && trivia[trivia.Count - 1].IsKind(SyntaxKind.EndOfLineTrivia)
        && trivia[trivia.Count - 2].IsKind(SyntaxKind.EndOfLineTrivia);

    private static TypeDeclarationSyntax RemovePrimaryConstructor(TypeDeclarationSyntax typeDeclaration, SyntaxTrivia lineFeed)
    {
        if (typeDeclaration.ParameterList is null)
            return typeDeclaration;

        var typeLeadingWhitespace = typeDeclaration
            .GetLeadingTrivia()
            .LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia));

        SyntaxTrivia[] openBraceTrivia = typeLeadingWhitespace.RawKind == 0
            ? [lineFeed]
            : [lineFeed, typeLeadingWhitespace];

        return typeDeclaration
            .WithParameterList(null)
            .WithOpenBraceToken(typeDeclaration.OpenBraceToken.WithLeadingTrivia(openBraceTrivia));
    }

    /// <summary>
    ///     The <c>// expression</c> comment above an evaluation, split after the first <c>||</c> so a long
    ///     disjunction reads as two lines. The first line follows whatever indent precedes it.
    /// </summary>
    private sealed class EvaluationComment(ExpressionSyntax expression, SyntaxTrivia lineFeed)
    {
        private readonly string[] _parts = expression.NormalizeWhitespace().ToFullString().Split([" || "], 2, StringSplitOptions.None);

        public SyntaxTrivia LineFeed => lineFeed;

        public SyntaxTriviaList Lines(SyntaxTrivia indent) =>
            _parts.Length == 1
                ? TriviaList(Comment($"// {_parts[0]}"), lineFeed)
                : TriviaList(
                    Comment($"// {_parts[0]} ||"), lineFeed,
                    indent, Comment($"//     {_parts[1]}"), lineFeed);
    }
}
