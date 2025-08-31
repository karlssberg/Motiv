using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.Model;
using Motiv.Generator.Model.Storage;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.Generation.SyntaxElements.ValueStorage;

public static class FieldAndPropertySyntax
{
    public static ImmutableArray<MemberDeclarationSyntax> CreateDeclarations(
        OrderedDictionary<IParameterSymbol, IFluentValueStorage> valueStorages) =>
        [..valueStorages.Values.Select(CreateDeclaration).OfType<MemberDeclarationSyntax>()];

    private static MemberDeclarationSyntax? CreateDeclaration(IFluentValueStorage valueStorage)
    {
        return valueStorage switch
        {
            FieldStorage { DefinitionExists: false } fieldStorage =>
                CreateFieldDeclaration(fieldStorage),
            PropertyStorage { DefinitionExists: false } propertyStorage =>
                CreatePropertyDeclaration(propertyStorage),
            _ => null
        };
    }

    private static FieldDeclarationSyntax CreateFieldDeclaration(FieldStorage fieldStorage)
    {
        return FieldDeclaration(
                VariableDeclaration(ParseTypeName(fieldStorage.Type.ToDynamicDisplayString(fieldStorage.ContainingNamespace)))
                    .AddVariables(VariableDeclarator(
                        Identifier(fieldStorage.IdentifierName))))
            .WithModifiers(TokenList(
                Token(SyntaxKind.PrivateKeyword),
                Token(SyntaxKind.ReadOnlyKeyword)));
    }

    private static PropertyDeclarationSyntax CreatePropertyDeclaration(PropertyStorage propertyStorage)
    {
        return PropertyDeclaration(
                ParseTypeName(propertyStorage.Type.ToDynamicDisplayString(propertyStorage.ContainingNamespace)),
                Identifier(propertyStorage.IdentifierName))
            .WithModifiers(TokenList(propertyStorage.Accessibility
                .AccessibilityToSyntaxKind()
                .Select(Token)))
            .WithAccessorList(
                AccessorList(
                    SingletonList(
                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)))));
    }


}
