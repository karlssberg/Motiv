using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentBuilder.Generation.SyntaxElements.Methods;
using Motiv.Generator.FluentBuilder.Model;
using Motiv.Generator.FluentBuilder.Model.Methods;
using Motiv.Generator.FluentBuilder.Model.Steps;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentBuilder.Generation.SyntaxElements;

public static class RootTypeDeclaration
{
    private static readonly SymbolDisplayFormat NameOnlyFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes)

    .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
    public static TypeDeclarationSyntax Create(FluentFactoryCompilationUnit file)
    {
        var rootMethodDeclarations = GetRootMethodDeclarations(file);

        var identifier = IdentifierName(file.RootType.ToDisplayString(NameOnlyFormat)).Identifier;
        TypeDeclarationSyntax typeDeclaration = file.TypeKind switch
        {
            TypeKind.Struct when file.IsRecord  =>
                RecordDeclaration(SyntaxKind.RecordStructDeclaration, Token(SyntaxKind.StructKeyword), identifier)
                    .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
                    .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken))
                    .WithModifiers(
                        TokenList(GetRootTypeModifiers(file).Append(Token(SyntaxKind.RecordKeyword)))),

            TypeKind.Struct =>
                StructDeclaration(identifier)
                    .WithModifiers(
                        TokenList(GetRootTypeModifiers(file))),

            TypeKind.Class when file.IsRecord =>
                RecordDeclaration(SyntaxKind.RecordDeclaration, Token(SyntaxKind.RecordKeyword), identifier)
                    .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
                    .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken))
                    .WithModifiers(
                        TokenList(GetRootTypeModifiers(file))),

            _ =>
                ClassDeclaration(identifier)
                    .WithModifiers(
                        TokenList(GetRootTypeModifiers(file)))
        };

        return typeDeclaration.WithMembers(
            List(rootMethodDeclarations.OfType<MemberDeclarationSyntax>()));
    }

    private static IEnumerable<SyntaxToken> GetRootTypeModifiers(FluentFactoryCompilationUnit file)
    {
        foreach (var syntaxKind in file.Accessibility.AccessibilityToSyntaxKind())
        {
            yield return Token(syntaxKind);
        }
        if (file.IsStatic)
        {
            yield return Token(SyntaxKind.StaticKeyword);
        }
        yield return Token(SyntaxKind.PartialKeyword);
    }

    private static IEnumerable<MethodDeclarationSyntax> GetRootMethodDeclarations(FluentFactoryCompilationUnit file)
    {
        return file.FluentMethods
            .Select<IFluentMethod, MethodDeclarationSyntax>(method => method switch
            {
                { Return: TargetTypeReturn } => FluentRootFactoryMethodDeclaration.Create(file.RootType.ContainingNamespace, method),
                MultiMethod multiMethod => FluentStepMethodDeclaration.Create(multiMethod, [], file.RootType.ContainingNamespace),
                _ => FluentStepMethodDeclaration.Create(method, [], file.RootType.ContainingNamespace)
            })
            .Select(method =>
            {
                return method
                    .WithModifiers(
                        TokenList(GetSyntaxTokens()));

                IEnumerable<SyntaxToken> GetSyntaxTokens()
                {
                    yield return Token(SyntaxKind.PublicKeyword);
                    yield return Token(SyntaxKind.StaticKeyword);
                }
            });
    }
}
