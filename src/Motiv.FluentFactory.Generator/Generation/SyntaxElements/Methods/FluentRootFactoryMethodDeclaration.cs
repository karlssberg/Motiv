using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.FluentFactory.Generator.Generation.Shared;
using Motiv.FluentFactory.Generator.Model.Methods;
using Motiv.FluentFactory.Generator.Model.Steps;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.FluentFactory.Generator.Generation.SyntaxElements.Methods;

public static class FluentRootFactoryMethodDeclaration
{
    public static MethodDeclarationSyntax Create(
        INamespaceSymbol currentNamespace,
        IFluentMethod method,
        INamedTypeSymbol rootType)
    {
        var fieldSourcedArguments = GetFieldSourcedArguments(method);

        var methodSourcedArguments = GetMethodSourcedArguments(method);

        var returnObjectExpression = TargetTypeObjectCreationExpression.Create(
            currentNamespace,
            method,
            fieldSourcedArguments,
            methodSourcedArguments);

        var methodDeclaration = GetMethodDeclarationSyntax(method, returnObjectExpression);

        // Check if we have method type parameters or target type parameters for non-generic root types
        var hasMethodTypeParameters = method.TypeParameters.Any();
        var shouldIncludeTargetTypeParameters = rootType?.IsGenericType != true &&
                                               method.Return is TargetTypeReturn targetTypeReturn &&
                                               targetTypeReturn.Constructor.ContainingType.IsGenericType;

        if (!hasMethodTypeParameters && !shouldIncludeTargetTypeParameters)
            return methodDeclaration;

        var typeParameterSyntaxes = GetTypeParameterSyntaxes(method, rootType);

        if (typeParameterSyntaxes.Length == 0)
            return methodDeclaration;

        var methodWithTypeParameters = methodDeclaration
            .WithTypeParameterList(
                TypeParameterList(SeparatedList([..typeParameterSyntaxes])));

        // Add constraint clauses for type parameters
        var constraintClauses = GetConstraintClauses(method, rootType);
        if (constraintClauses.Length > 0)
        {
            methodWithTypeParameters = methodWithTypeParameters
                .WithConstraintClauses(List(constraintClauses));
        }

        return methodWithTypeParameters;
    }

    private static ImmutableArray<TypeParameterSyntax> GetTypeParameterSyntaxes(IFluentMethod method, INamedTypeSymbol? rootType)
    {
        // For generic root types, don't add type parameters that are already defined by the root type
        // For non-generic root types, we need to include all target type parameters
        var shouldIncludeTargetTypeParameters = rootType?.IsGenericType != true;

        var targetTypeParameterSyntaxes = shouldIncludeTargetTypeParameters
            ? method.Return switch
            {
                TargetTypeReturn targetTypeReturn => targetTypeReturn.Constructor.ContainingType.OriginalDefinition.TypeParameters
                    .Select(typeParameterSymbol => typeParameterSymbol.ToTypeParameterSyntax()),
                _ => []
            }
            : [];

        var accumulatedTypeParameterSyntaxes = method.TypeParameters
            .Select(typeParameter => typeParameter.TypeParameterSymbol.ToTypeParameterSyntax());

        var allTypeParameters = accumulatedTypeParameterSyntaxes
            .Concat(targetTypeParameterSyntaxes)
            .DistinctBy(typeParameter => typeParameter.Identifier.Text);

        return [..allTypeParameters];
    }

    private static ImmutableArray<TypeParameterConstraintClauseSyntax> GetConstraintClauses(IFluentMethod method, INamedTypeSymbol? rootType)
    {
        // For generic root types, don't add constraint clauses for type parameters already defined by the root type
        // For non-generic root types, we need to include all target type constraints
        var shouldIncludeTargetTypeConstraints = rootType?.IsGenericType != true;

        var constraintClauses = new List<TypeParameterConstraintClauseSyntax>();

        // Get target type parameters and their constraints
        if (shouldIncludeTargetTypeConstraints && method.Return is TargetTypeReturn targetTypeReturn)
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

    private static MethodDeclarationSyntax GetMethodDeclarationSyntax(IFluentMethod method,
        ObjectCreationExpressionSyntax returnObjectExpression)
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
                FluentMethodSummaryDocXml.Create(
                [
                    method.DocumentationSummary,
                    ..FluentMethodSummaryDocXml.GenerateCandidateConstructorTypeSeeAlsoLinks(method.Return.CandidateConstructors)
                ]));

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

        return methodDeclaration;
    }

    private static IEnumerable<ArgumentSyntax> GetMethodSourcedArguments(IFluentMethod method)
    {
        return method.MethodParameters
            .Select(parameter => IdentifierName(parameter.ParameterSymbol.Name.ToCamelCase()))
            .Select(Argument);
    }

    private static IEnumerable<ArgumentSyntax> GetFieldSourcedArguments(IFluentMethod method)
    {
        return method.AvailableParameterFields
            .Select(parameter => IdentifierName(parameter.ParameterSymbol.Name.ToCamelCase()))
            .Select(Argument);
    }
}
