using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentFactory.Model;
using Motiv.Generator.FluentFactory.Model.Steps;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentFactory.Generation.SyntaxElements;

public static class CompilationUnit
{
    public static SyntaxNode CreateCompilationUnit(
        FluentFactoryCompilationUnit file)
    {

        var usingDirectiveSyntaxes = file.Usings
            .Where(namespaceSymbol => !namespaceSymbol.IsGlobalNamespace)
            .Where(namespaceSymbol => namespaceSymbol.OriginalDefinition.ToDisplayString() != file.Namespace)
            .Select(dep =>
                UsingDirective(
                    ParseName(dep.ToDisplayString())));

        var members = GetMembers(file);

        return CompilationUnit()
            .WithUsings(List(usingDirectiveSyntaxes))
            .WithMembers(List(members));
    }

    private static IEnumerable<MemberDeclarationSyntax> GetMembers(FluentFactoryCompilationUnit file)
    {
        var rootType = RootTypeDeclaration.Create(file);
        var namespacesGroups = file.FluentSteps
            .GroupBy(
                step => step.Namespace,
                SymbolEqualityComparer.Default)
            .Select(stepsInNamespace =>
            {
                var declarations = CreateTypeDeclarations(stepsInNamespace);
                return
                (
                    namespaces: stepsInNamespace.Key as INamespaceSymbol,
                    declarations: SymbolEqualityComparer.Default.Equals(file.RootType.ContainingNamespace,
                        stepsInNamespace.Key)
                        ? [rootType, ..declarations]
                        : declarations
                );
            });

        var memberDeclarations = namespacesGroups
            .SelectMany(tuple => MaybeEncapsulateInNamespace(tuple.namespaces, tuple.declarations))
            .ToArray();

        return DoFluentStepsShareTheRootNamespace()
            ? memberDeclarations
            : [..MaybeEncapsulateInNamespace(file.RootType.ContainingNamespace, [rootType]), ..memberDeclarations];

        IEnumerable<TypeDeclarationSyntax> CreateTypeDeclarations(
            IEnumerable<IFluentStep> fluentSteps)
        {
            foreach (var step in fluentSteps)
            {
                yield return
                    step switch {
                        ExistingTypeFluentStep existingPartialTypeStep =>
                            ExistingPartialTypeStepDeclaration.Create(existingPartialTypeStep),
                        RegularFluentStep regularFluentStep =>
                            FluentStepDeclaration.Create(regularFluentStep),
                        _ =>
                            throw new NotSupportedException($"Step type {step.GetType()} is not supported.")
                    };
            }
        }

        IEnumerable<MemberDeclarationSyntax> MaybeEncapsulateInNamespace(INamespaceSymbol? namespaces, IEnumerable<TypeDeclarationSyntax> declarations)
        {
            if (namespaces is null || namespaces.IsGlobalNamespace)
                return declarations;

            return
                [
                    NamespaceDeclaration(ParseName(namespaces.ToDisplayString()))
                        .WithMembers(List([..declarations.OfType<MemberDeclarationSyntax>()]))
                ];
        }

        bool DoFluentStepsShareTheRootNamespace()
        {
            var rootNamespace = file.RootType.ContainingNamespace;
            return
                file.FluentSteps.Any(
                    step => step is not ExistingTypeFluentStep existingTypeFluentStep
                            || SymbolEqualityComparer.Default.Equals(
                                existingTypeFluentStep.Namespace,
                                 rootNamespace));
        }
    }
}
