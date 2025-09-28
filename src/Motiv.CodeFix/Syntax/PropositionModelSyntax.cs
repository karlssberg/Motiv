using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix.Syntax;

public static class PropositionModelSyntax
{
    public static TypeDeclarationSyntax Create(string modelName, ImmutableArray<ISymbol> variableSymbols)
    {
        var variables = variableSymbols
            .Select((string Name, ITypeSymbol TypeSymbol) (symbol) =>
                symbol switch
                {
                    IParameterSymbol parameterSymbol => (parameterSymbol.Name, parameterSymbol.Type),
                    ILocalSymbol localSymbol => (localSymbol.Name, localSymbol.Type),
                    IFieldSymbol fieldSymbol => (fieldSymbol.Name, fieldSymbol.Type),
                    IPropertySymbol propertySymbol => (propertySymbol.Name, propertySymbol.Type),
                    _ => throw new InvalidOperationException("Unknown symbol type")
                })
            .ToImmutableList();

        var autoPropertySyntax = AutoPropertySyntax();

        var propertyDeclarationSyntaxes = CreateProperties(variables, autoPropertySyntax);

        var constructor = CreateConstructor(modelName, variables);

        return ClassDeclaration(modelName)
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithMembers(
                [
                    constructor,
                    ..propertyDeclarationSyntaxes
                ])
            .NormalizeWhitespace();
    }

    private static IEnumerable<PropertyDeclarationSyntax> CreateProperties(ImmutableList<(string Name, ITypeSymbol TypeSymbol)> variables, SyntaxList<AccessorDeclarationSyntax> autoPropertySyntax)
    {
        return variables
            .Select(v =>
                PropertyDeclaration(ParseTypeName(v.TypeSymbol.GetCSharpTypeName()), v.Name.Capitalize())
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                    .WithAccessorList(AccessorList(List([AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken))]))));
    }

    private static ConstructorDeclarationSyntax CreateConstructor(string modelName, ImmutableList<(string Name, ITypeSymbol TypeSymbol)> variables)
    {
        return ConstructorDeclaration(modelName)
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(
                ParameterList(
                [..
                    variables.Select(v =>
                        Parameter(Identifier(v.Name))
                            .WithType(ParseTypeName(v.TypeSymbol.GetCSharpTypeName())))
                ]))
            .WithBody(
                Block(
                    List<StatementSyntax>(
                        variables.Select(v =>
                            ExpressionStatement(
                                AssignmentExpression(
                                    SyntaxKind.SimpleAssignmentExpression,
                                    IdentifierName(v.Name.Capitalize()),
                                    IdentifierName(v.Name)))))));
    }

    private static SyntaxList<AccessorDeclarationSyntax> AutoPropertySyntax(
        SyntaxKind getterKind = SyntaxKind.GetAccessorDeclaration,
        SyntaxKind setterKind = SyntaxKind.SetAccessorDeclaration)
    {
        return List(
        [
            AccessorDeclaration(getterKind).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
            AccessorDeclaration(setterKind).WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
        ]);
    }
}
