using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix.Syntax;

/// <summary>
///     Abstract base for building a class that extends Spec&lt;TModel&gt; with a primary constructor.
/// </summary>
public abstract class SpecClassDeclaration(SyntaxContext syntaxContext, string propositionName)
{
    protected SyntaxContext SyntaxContext => syntaxContext;
    protected string PropositionName => propositionName;

    public TypeDeclarationSyntax Build()
    {
        var baseType = BuildBaseType();

        var classDeclaration = ClassDeclaration(propositionName)
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(BuildParameterList())
            .WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(baseType)));

        classDeclaration = AddClassBody(classDeclaration);

        var normalized = classDeclaration
            .NormalizeWhitespace(eol: syntaxContext.LineFeed.ToString());

        return FormatOutput(normalized);
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
