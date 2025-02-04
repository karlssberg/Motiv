using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentBuilder.FluentModel.Methods;
using Motiv.Generator.FluentBuilder.FluentModel.Steps;
using Motiv.Generator.FluentBuilder.Generation.SyntaxElements.Step.Fields;
using Motiv.Generator.FluentBuilder.Generation.SyntaxElements.Step.Methods;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Motiv.Generator.FluentBuilder.FluentModel.FluentParameterResolution.ValueLocationType;

namespace Motiv.Generator.FluentBuilder.Generation.SyntaxElements.Step;

public static class ExistingPartialTypeStepDeclaration
{
    public static TypeDeclarationSyntax Create(
        ExistingTypeFluentStep step)
    {
        var methodDeclarationSyntaxes = step.FluentMethods
            .Select<IFluentMethod, MethodDeclarationSyntax>(method => ExistingPartialTypeMethodDeclaration.Create(method, step));

        var parameterFieldDeclaration = step.KnownConstructorParameters
            .Where(parameter =>
                step.ParameterStoreMembers.TryGetValue(parameter, out var parameterResolution)
                && parameterResolution is
                {
                    ResolutionType: Member,
                    ExistingFieldSymbol: null,
                    ExistingPropertySymbol: null
                })
            .Select(parameter =>
                ParameterFieldDeclarations.CreateWithInitialization(
                    parameter,
                    IdentifierName(parameter.Name),
                    step.ExistingStepConstructor!.ContainingNamespace));

        var typeDeclaration = CreateTypeDeclarationSyntax(step, IdentifierName(step.Name).Identifier)
            .WithMembers(List<MemberDeclarationSyntax>([
                ..parameterFieldDeclaration,
                ..methodDeclarationSyntaxes,
            ]));

        return typeDeclaration;
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
                        TokenList(GetRootTypeModifiers(step))),

            TypeKind.Class =>
                ClassDeclaration(identifier)
                    .WithModifiers(
                        TokenList(GetRootTypeModifiers(step))),

            TypeKind.Struct  when step.IsRecord=>
                StructDeclaration(identifier)
                    .WithModifiers(
                        TokenList(GetRootTypeModifiers(step).Append(Token(SyntaxKind.RecordKeyword)))),

            _ =>
                StructDeclaration(identifier)
                    .WithModifiers(
                        TokenList(GetRootTypeModifiers(step))),
        };
    }

    private static IEnumerable<SyntaxToken> GetRootTypeModifiers(IFluentStep step)
    {
        return step.Accessibility
            .AccessibilityToSyntaxKind()
            .Select(Token)
            .Append(Token(SyntaxKind.PartialKeyword));
    }
}
