using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.FluentFactory.Generator.Generation.Shared;
using Motiv.FluentFactory.Generator.Model;
using Motiv.FluentFactory.Generator.Model.Methods;
using Motiv.FluentFactory.Generator.Model.Steps;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.FluentFactory.Generator.Generation.SyntaxElements.Methods;

public static class FluentStepMethodDeclaration
{
    public static MethodDeclarationSyntax Create(
        MultiMethod multiMethod,
        ParameterSequence knownConstructorParameters,
        INamespaceSymbol currentNamespace,
        ImmutableArray<ITypeParameterSymbol>? ambientTypeParameters = null)
    {
        var stepActivationArgs = CreateStepConstructorArguments(multiMethod, knownConstructorParameters);

        var returnObjectExpression = FluentStepCreationExpression.Create(currentNamespace, multiMethod, stepActivationArgs);

        return CreateMethodDeclaration(multiMethod, knownConstructorParameters, returnObjectExpression, ambientTypeParameters ?? []);
    }

    public static MethodDeclarationSyntax Create(
        IFluentMethod method,
        ParameterSequence knownConstructorParameters,
        INamespaceSymbol currentNamespace,
        ImmutableArray<ITypeParameterSymbol>? ambientTypeParameters = null)
    {
        var stepActivationArgs = CreateStepConstructorArguments(method, knownConstructorParameters);

        var returnObjectExpression = FluentStepCreationExpression.Create(currentNamespace, method, stepActivationArgs);

        return CreateMethodDeclaration(method, knownConstructorParameters, returnObjectExpression, ambientTypeParameters ?? []);
    }

    private static List<object?> GetDocumentationLinesWithParameters(IFluentMethod method)
    {
        var lines = new List<object?>();

        // Add the main documentation summary
        if (!string.IsNullOrWhiteSpace(method.DocumentationSummary))
        {
            lines.Add(method.DocumentationSummary?.Trim());
            lines.Add("");
        }

        // Add constructor type information
        lines.AddRange(FluentMethodSummaryDocXml.GenerateCandidateConstructorTypeSeeAlsoLinks(method.Return.CandidateConstructors).Cast<object?>());

        return lines;
    }

    private static MethodDeclarationSyntax CreateMethodDeclaration(
        IFluentMethod method,
        ParameterSequence knownConstructorParameters,
        ObjectCreationExpressionSyntax returnObjectExpression,
        ImmutableArray<ITypeParameterSymbol> ambientTypeParameters)
    {
        var methodDeclaration = MethodDeclaration(
                returnObjectExpression.Type,
                Identifier(method.Name))
            .WithAttributeLists(
                SingletonList(
                    AttributeList(
                        SingletonSeparatedList(AggressiveInliningAttributeSyntax.Create()))))
            .WithModifiers(
                TokenList(
                    Token(SyntaxKind.PublicKeyword)))
            .WithBody(Block(ReturnStatement(returnObjectExpression)))
            .WithLeadingTrivia(
                method switch
                {
                    { ParameterDocumentation: not null, MethodParameters.Length: > 0 } =>
                        FluentMethodSummaryDocXml.CreateWithParameters(
                            GetDocumentationLinesWithParameters(method),
                            method.ParameterDocumentation,
                            method.MethodParameters.Select(p => p.ParameterSymbol.Name.ToCamelCase())),
                    _ =>
                        FluentMethodSummaryDocXml.Create(GetDocumentationLinesWithParameters(method))
                });

        if (method.MethodParameters.Length > 0)
        {
            methodDeclaration = methodDeclaration
                .WithParameterList(
                    ParameterList(SeparatedList(
                        method.MethodParameters
                            .Select(parameter =>
                                Parameter(
                                        Identifier(parameter.ParameterSymbol.Name.ToCamelCase()))
                                    .WithModifiers(TokenList(Token(SyntaxKind.InKeyword)))
                                    .WithType(
                                        IdentifierName(parameter.ParameterSymbol.Type.ToDynamicDisplayString(method.RootNamespace)))))));
        }

        if (!method.TypeParameters.Any())
            return methodDeclaration;

        // Get root type parameters to exclude from method-level type parameters
        var rootTypeParameters = ambientTypeParameters
            .Select(tp => new FluentTypeParameter(tp))
            .ToArray() ?? [];
        var rootTypeParametersSet = new HashSet<FluentTypeParameter>(rootTypeParameters);

        var typeParameterSyntaxes = method.TypeParameters
            .Except(knownConstructorParameters
                .SelectMany(parameter => parameter.Type.GetGenericTypeParameters())
                .Select(genericTypeParameters => new FluentTypeParameter(genericTypeParameters)))
            .Except(rootTypeParametersSet) // Exclude root type parameters
            .Select(fluentTypeParameter => fluentTypeParameter.TypeParameterSymbol.ToTypeParameterSyntax())
            .ToImmutableArray();

        if (typeParameterSyntaxes.Length == 0)
            return methodDeclaration;

        var methodWithTypeParameters = methodDeclaration.WithTypeParameterList(
            TypeParameterList(SeparatedList([..typeParameterSyntaxes])));

        // Add constraint clauses for type parameters
        var constraintClauses = GetConstraintClauses(method, ambientTypeParameters);
        if (constraintClauses.Length > 0)
        {
            methodWithTypeParameters = methodWithTypeParameters
                .WithConstraintClauses(List(constraintClauses));
        }

        return methodWithTypeParameters;
    }

    private static ImmutableArray<TypeParameterConstraintClauseSyntax> GetConstraintClauses(IFluentMethod method, ImmutableArray<ITypeParameterSymbol> ambientTypeParameters)
    {
        var constraintClauses = new List<TypeParameterConstraintClauseSyntax>();

        // Get target type parameters and their constraints for non-generic root types
        if (ambientTypeParameters.IsEmpty && method.Return is TargetTypeReturn targetTypeReturn &&
            targetTypeReturn.Constructor.ContainingType.IsGenericType)
        {
            foreach (var typeParam in targetTypeReturn.Constructor.ContainingType.OriginalDefinition.TypeParameters)
            {
                var constraints = new List<TypeParameterConstraintSyntax>();

                // Add value type constraint
                if (typeParam.HasValueTypeConstraint)
                {
                    constraints.Add(ClassOrStructConstraint(SyntaxKind.StructConstraint));
                }

                // Add reference type constraint
                if (typeParam.HasReferenceTypeConstraint)
                {
                    constraints.Add(ClassOrStructConstraint(SyntaxKind.ClassConstraint));
                }

                // Add constructor constraint
                if (typeParam.HasConstructorConstraint)
                {
                    constraints.Add(ConstructorConstraint());
                }

                // Add type constraints
                foreach (var constraintType in typeParam.ConstraintTypes)
                {
                    var typeName = constraintType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
                        .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
                    constraints.Add(TypeConstraint(ParseTypeName(typeName)));
                }

                if (constraints.Count > 0)
                {
                    constraintClauses.Add(
                        TypeParameterConstraintClause(
                            IdentifierName(typeParam.Name))
                        .WithConstraints(SeparatedList(constraints)));
                }
            }
        }

        // Add constraints from method type parameters
        foreach (var typeParam in method.TypeParameters)
        {
            var constraints = new List<TypeParameterConstraintSyntax>();

            // Add value type constraint
            if (typeParam.TypeParameterSymbol.HasValueTypeConstraint)
            {
                constraints.Add(ClassOrStructConstraint(SyntaxKind.StructConstraint));
            }

            // Add reference type constraint
            if (typeParam.TypeParameterSymbol.HasReferenceTypeConstraint)
            {
                constraints.Add(ClassOrStructConstraint(SyntaxKind.ClassConstraint));
            }

            // Add constructor constraint
            if (typeParam.TypeParameterSymbol.HasConstructorConstraint)
            {
                constraints.Add(ConstructorConstraint());
            }

            // Add type constraints
            foreach (var constraintType in typeParam.TypeParameterSymbol.ConstraintTypes)
            {
                var typeName = constraintType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
                    .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
                constraints.Add(TypeConstraint(ParseTypeName(typeName)));
            }

            if (constraints.Count > 0)
            {
                constraintClauses.Add(
                    TypeParameterConstraintClause(
                        IdentifierName(typeParam.TypeParameterSymbol.Name))
                    .WithConstraints(SeparatedList(constraints)));
            }
        }

        return [..constraintClauses];
    }

    private static IEnumerable<ArgumentSyntax> CreateStepConstructorArguments(
        IFluentMethod method,
        ParameterSequence knownConstructorParameters)
    {
        return knownConstructorParameters
            .Select(parameter =>
                Argument(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ThisExpression(),
                        IdentifierName(parameter.Name.ToParameterFieldName()))))
            .Concat(
                method.MethodParameters.Select(p => p.ParameterSymbol.Name.ToCamelCase())
                    .Select(IdentifierName)
                    .Select(Argument));
    }
}
