using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentBuilder.FluentModel.Methods;
using Motiv.Generator.FluentBuilder.FluentModel.Steps;
using Motiv.Generator.FluentBuilder.Generation.SyntaxElements.Step.Constructors;
using Motiv.Generator.FluentBuilder.Generation.SyntaxElements.Step.Fields;
using Motiv.Generator.FluentBuilder.Generation.SyntaxElements.Step.Methods;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentBuilder.Generation.SyntaxElements.Step;

public static class FluentStepDeclaration
{
    public static StructDeclarationSyntax Create(
        RegularFluentStep step)
    {
        var methodDeclarationSyntaxes = step.FluentMethods
            .Select<IFluentMethod, MethodDeclarationSyntax>(method =>
                method switch
                {
                    CreateMethod createMethod => FluentFactoryMethodDeclaration.Create(createMethod, step),
                    MultiMethod multiMethod => FluentStepMethodDeclaration.Create(multiMethod, step),
                    _ => FluentStepMethodDeclaration.Create(method, step)
                });

        var fieldDeclaration = step.KnownConstructorParameters
            .Select(parameter => ParameterFieldDeclarations.Create(parameter, step.RootType.ContainingNamespace));

        var constructor = FluentStepConstructorDeclaration.Create(step);

        NameSyntax name = IdentifierName(((IFluentReturn)step).IdentifierDisplayString(step.Namespace));

        var identifier = name is GenericNameSyntax genericName
            ? genericName.Identifier
            : ((SimpleNameSyntax)name).Identifier;

        var structDeclaration = StructDeclaration(identifier)
            .WithModifiers(
                TokenList(
                    Token(SyntaxKind.PublicKeyword)))
            .WithMembers(List<MemberDeclarationSyntax>([
                ..fieldDeclaration,
                constructor,
                ..methodDeclarationSyntaxes,
            ]));

        return structDeclaration;
    }
}
