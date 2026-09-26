using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix.Syntax;

/// <summary>
///     Abstract base for building a class that extends Spec&lt;TModel&gt; with a primary constructor.
/// </summary>
/// <param name="syntaxContext">The syntax context for trivia handling.</param>
/// <param name="propositionName">The name of the class.</param>
/// <param name="typeParameters">The type parameters the class declares, if the expression depends on any.</param>
/// <param name="instanceField">The static field holding the class's own instance, when it declares type parameters.</param>
public abstract class SpecClassDeclaration(
    SyntaxContext syntaxContext,
    string propositionName,
    SpecTypeParameters typeParameters,
    MemberDeclarationSyntax? instanceField = null)
{
    protected SyntaxContext SyntaxContext => syntaxContext;
    protected string PropositionName => propositionName;

    /// <summary>The class's name with its type arguments, as the class refers to itself.</summary>
    protected string SpecTypeName => typeParameters.Qualify(propositionName);

    public TypeDeclarationSyntax Build()
    {
        var baseType = BuildBaseType();

        var classDeclaration = ClassDeclaration(propositionName)
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(BuildParameterList())
            .WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(baseType)));

        classDeclaration = AddClassBody(classDeclaration);
        classDeclaration = typeParameters.ApplyTo(classDeclaration);

        var normalized = classDeclaration
            .NormalizeWhitespace(eol: syntaxContext.LineFeed.ToString());

        var formatted = FormatOutput(normalized);
        return instanceField is null ? formatted : AddInstanceField(formatted, instanceField);
    }

    /// <summary>
    ///     Adds the already formatted instance field as the class's first member, giving a semicolon-terminated
    ///     class a body to hold it.
    /// </summary>
    private TypeDeclarationSyntax AddInstanceField(TypeDeclarationSyntax classDeclaration, MemberDeclarationSyntax field)
    {
        var lineFeed = syntaxContext.LineFeed;

        if (classDeclaration.OpenBraceToken.IsKind(SyntaxKind.None))
        {
            classDeclaration = classDeclaration
                .WithSemicolonToken(Token(SyntaxKind.None))
                .WithOpenBraceToken(Token(TriviaList(lineFeed), SyntaxKind.OpenBraceToken, TriviaList(lineFeed)))
                .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken));
        }

        var separator = classDeclaration.Members.Any() ? TriviaList(lineFeed, lineFeed) : TriviaList(lineFeed);
        return classDeclaration.WithMembers(
            classDeclaration.Members.Insert(0, field.WithTrailingTrivia(separator)));
    }

    private PrimaryConstructorBaseTypeSyntax BuildBaseType()
    {
        var outerLambda = ParenthesizedLambdaExpression()
            .WithParameterList(ParameterList());

        outerLambda = AttachLambdaBody(outerLambda);

        return PrimaryConstructorBaseType(
            GenericName(
                Identifier("Spec"),
                TypeArgumentList(
                    SingletonSeparatedList(GetModelType()))),
            ArgumentList(
                SingletonSeparatedList(
                    Argument(outerLambda))));
    }

    protected abstract TypeSyntax GetModelType();

    protected abstract ParenthesizedLambdaExpressionSyntax AttachLambdaBody(
        ParenthesizedLambdaExpressionSyntax lambda);

    protected virtual ParameterListSyntax BuildParameterList() => ParameterList();

    protected virtual ClassDeclarationSyntax AddClassBody(ClassDeclarationSyntax classDeclaration) =>
        classDeclaration
            .WithOpenBraceToken(Token(SyntaxKind.None))
            .WithCloseBraceToken(Token(SyntaxKind.None))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

    protected abstract TypeDeclarationSyntax FormatOutput(ClassDeclarationSyntax normalized);
}
