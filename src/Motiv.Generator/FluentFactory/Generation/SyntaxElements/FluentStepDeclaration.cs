using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentFactory.Generation.SyntaxElements.Constructors;
using Motiv.Generator.FluentFactory.Generation.SyntaxElements.Methods;
using Motiv.Generator.FluentFactory.Generation.SyntaxElements.ValueStorage;
using Motiv.Generator.FluentFactory.Model.Methods;
using Motiv.Generator.FluentFactory.Model.Steps;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentFactory.Generation.SyntaxElements;

public static class FluentStepDeclaration
{
    public static StructDeclarationSyntax Create(
        RegularFluentStep step)
    {
        var methodDeclarationSyntaxes = step.FluentMethods
            .Select<IFluentMethod, MethodDeclarationSyntax>(method =>
                method switch
                {
                    CreationMethod createMethod => FluentFactoryMethodDeclaration.Create(createMethod, step),
                    MultiMethod multiMethod => FluentStepMethodDeclaration.Create(multiMethod, step.KnownConstructorParameters, step.Namespace),
                    _ => FluentStepMethodDeclaration.Create(method, step.KnownConstructorParameters, step.Namespace)
                });

        var fieldDeclarations = FieldAndPropertySyntax.CreateDeclarations(step.ValueStorage);

        var constructor = FluentStepConstructorDeclaration.Create(step);

        NameSyntax name = IdentifierName(((IFluentReturn)step).IdentifierDisplayString(step.Namespace));

        var identifier = name is GenericNameSyntax genericName
            ? genericName.Identifier
            : ((SimpleNameSyntax)name).Identifier;

        SyntaxTokenList accessibilityToken = step.Accessibility switch
        {
            Accessibility.Public => [Token(SyntaxKind.PublicKeyword)],
            Accessibility.Private => [Token(SyntaxKind.PrivateKeyword)],
            Accessibility.Protected => [Token(SyntaxKind.ProtectedKeyword)],
            Accessibility.Internal => [Token(SyntaxKind.InternalKeyword)],
            Accessibility.ProtectedOrInternal => [Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.InternalKeyword)],
            Accessibility.ProtectedAndInternal => [Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ProtectedKeyword)],
            _ => [Token(SyntaxKind.PublicKeyword)]
        };

        // Create XML documentation for the struct
        var xmlDocTrivia = FluentMethodSummaryDocXml.Create(
            [
                ..FluentMethodSummaryDocXml.GenerateCandidateConstructorTypeSeeAlsoLinks(step.CandidateConstructors)
            ]);

        var structDeclaration = StructDeclaration(identifier)
        .WithModifiers(accessibilityToken)
        .WithLeadingTrivia(xmlDocTrivia)
        .WithMembers(List<MemberDeclarationSyntax>([
            ..fieldDeclarations,
            constructor,
            ..methodDeclarationSyntaxes,
        ]));

        return structDeclaration;
    }
}
