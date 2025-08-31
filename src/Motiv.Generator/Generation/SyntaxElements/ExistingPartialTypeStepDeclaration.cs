using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.Generation.SyntaxElements.Methods;
using Motiv.Generator.Generation.SyntaxElements.ValueStorage;
using Motiv.Generator.Model.Methods;
using Motiv.Generator.Model.Steps;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.Generation.SyntaxElements;

public static class ExistingPartialTypeStepDeclaration
{
    public static TypeDeclarationSyntax Create(
        ExistingTypeFluentStep step)
    {
        var methodDeclarationSyntaxes = step.FluentMethods
            .Select<IFluentMethod, MethodDeclarationSyntax>(method => ExistingPartialTypeMethodDeclaration.Create(method, step));

        var parameterFieldDeclaration = FieldAndPropertySyntax.CreateDeclarations(step.ValueStorage);

        var identifier = IdentifierName(step.Name).Identifier;
        return CreateTypeDeclarationSyntax(step, identifier)
            .WithMembers(List<MemberDeclarationSyntax>([
                ..parameterFieldDeclaration,
                ..methodDeclarationSyntaxes,
            ]));
    }

    private static TypeDeclarationSyntax CreateTypeDeclarationSyntax(IFluentStep step, SyntaxToken identifier)
    {
        return step.TypeKind switch
        {
            TypeKind.Class when step.IsRecord =>
                RecordDeclaration(
                        SyntaxKind.RecordDeclaration,
                        Token(SyntaxKind.RecordKeyword),
                        identifier)
                    .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
                    .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken))
                    .WithModifiers(
                        TokenList(GetModifiers(step))),

            TypeKind.Class =>
                ClassDeclaration(identifier)
                    .WithModifiers(
                        TokenList(GetModifiers(step))),

            TypeKind.Struct when step.IsRecord =>
                StructDeclaration(identifier)
                    .WithModifiers(
                        TokenList(GetModifiers(step).Append(Token(SyntaxKind.RecordKeyword)))),

            _ =>
                StructDeclaration(identifier)
                    .WithModifiers(
                        TokenList(GetModifiers(step))),
        };
    }

    private static IEnumerable<SyntaxToken> GetModifiers(IFluentStep step)
    {
        if (step is ExistingTypeFluentStep existingStep)
        {
            var originalModifiers = existingStep.ConstructorContext.OriginalTypeModifiers;

            // Filter out 'partial' from original modifiers since we'll add it back
            var modifiersToKeep = originalModifiers.Where(m => !m.IsKind(SyntaxKind.PartialKeyword));

            return modifiersToKeep.Append(Token(SyntaxKind.PartialKeyword));
        }

        // Fallback to just accessibility + partial for non-existing types
        return step.Accessibility
            .AccessibilityToSyntaxKind()
            .Select(Token)
            .Append(Token(SyntaxKind.PartialKeyword));
    }
}
