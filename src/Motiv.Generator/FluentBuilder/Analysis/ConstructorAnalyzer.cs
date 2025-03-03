
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentBuilder.Model;
using Motiv.Generator.FluentBuilder.Model.Storage;

namespace Motiv.Generator.FluentBuilder.Analysis;

public class ConstructorAnalyzer(SemanticModel semanticModel)
{
    public OrderedDictionary<IParameterSymbol, IFluentValueStorage> FindParameterValueStorage(IMethodSymbol constructor)
    {
        var results =
            new OrderedDictionary<IParameterSymbol, IFluentValueStorage>(constructor.Parameters
                .Select(parameterSymbol =>
                    new KeyValuePair<IParameterSymbol, IFluentValueStorage>(
                        parameterSymbol,
                        new NullStorage(semanticModel.Compilation.GetSpecialType(SpecialType.System_Void)))),
                FluentParameterComparer.Default);

        var containingType = constructor.ContainingType;

        // For records, primary constructor parameters automatically become properties
        if (containingType.IsRecord)
        {
            foreach (var parameter in constructor.Parameters)
            {
                var property = containingType.GetMembers()
                    .OfType<IPropertySymbol>()
                    .FirstOrDefault(p => p.Name.Equals(parameter.Name, StringComparison.OrdinalIgnoreCase));

                if (property is null) continue;

                results[parameter] =
                    new PropertyStorage(property.Name, property.Type, constructor.ContainingNamespace)
                    {
                        DefinitionExists = true
                    };
            }
            return results;
        }

        // For classes with primary constructors, look for property declarations
        var syntaxNode = constructor.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        if (syntaxNode is TypeDeclarationSyntax { ParameterList: not null })
        {
            PopulateWithPrimaryConstructorParametersDirectAccess(constructor, results, constructor.ContainingNamespace);
            PopulateWithMembersInitializedFromPrimaryConstructors(constructor, results, constructor.ContainingNamespace);

            return results;
        }

        // For explicit constructors, analyze the constructor body
        if (syntaxNode is not ConstructorDeclarationSyntax ctorSyntax) return results;

        // Analyze constructor body for assignments
        if (ctorSyntax.Body is not null)
        {
            PopulateWithRelevantFieldAndPropertyReferences(ctorSyntax, results, constructor.ContainingNamespace);
        }

        // Look for property/field initializations in constructor initializer (i.e. : base() and : this())
        var initializer = ctorSyntax.Initializer;
        if (initializer == null) return results;
        var initializerMethod = (IMethodSymbol?)semanticModel.GetSymbolInfo(initializer).Symbol;
        if (initializerMethod == null) return results;
        var baseResults = FindParameterValueStorage(initializerMethod);

        for (var i = 0; i < initializer.ArgumentList.Arguments.Count; i++)
        {
            var argument = initializer.ArgumentList.Arguments[i];
            if (semanticModel.GetSymbolInfo(argument.Expression).Symbol is not IParameterSymbol parameterSymbol ||
                !results.ContainsKey(parameterSymbol)) continue;

            if (i >= initializerMethod.Parameters.Length) continue;

            var baseParam = initializerMethod.Parameters[i];
            if (baseResults.TryGetValue(baseParam, out var baseProperty))
            {
                results[parameterSymbol] = baseProperty;
            }
        }

        return results;
    }

    private void PopulateWithRelevantFieldAndPropertyReferences(
        ConstructorDeclarationSyntax ctorSyntax,
        OrderedDictionary<IParameterSymbol, IFluentValueStorage> results,
        INamespaceSymbol constructorContainingNamespace)
    {
        var assignments = ctorSyntax.Body?
            .DescendantNodes()
            .OfType<AssignmentExpressionSyntax>();

        foreach (var assignment in assignments ?? [])
        {
            var rightSymbol = semanticModel.GetSymbolInfo(assignment.Right).Symbol;
            if (rightSymbol is not IParameterSymbol paramSymbol ||
                !results.ContainsKey(paramSymbol)) continue;

            var memberSymbol = semanticModel.GetSymbolInfo(assignment.Left).Symbol;
            results[paramSymbol] = memberSymbol switch
            {
                IPropertySymbol propertySymbol =>
                    new PropertyStorage(propertySymbol.Name, propertySymbol.Type, constructorContainingNamespace)
                    {
                        DefinitionExists = true
                    },
                IFieldSymbol fieldSymbol =>
                    new FieldStorage(fieldSymbol.Name, fieldSymbol.Type, constructorContainingNamespace)
                    {
                        DefinitionExists = true
                    },
                _ => results[paramSymbol]
            };
        }
    }

    private void PopulateWithPrimaryConstructorParametersDirectAccess(
        IMethodSymbol primaryConstructor,
        OrderedDictionary<IParameterSymbol, IFluentValueStorage> result,
        INamespaceSymbol constructorContainingNamespace)
    {
        foreach (var parameter in primaryConstructor.Parameters)
        {
            result[parameter] =
                new PrimaryConstructorParameterStorage(
                    parameter.Name,
                    parameter.Type,
                    constructorContainingNamespace)
                {
                    DefinitionExists = true
                };
        }
    }

    private void PopulateWithMembersInitializedFromPrimaryConstructors(
        IMethodSymbol primaryConstructor,
        OrderedDictionary<IParameterSymbol, IFluentValueStorage> result,
        INamespaceSymbol constructorContainingNamespace)
    {
        foreach (var member in primaryConstructor.ContainingType.GetMembers())
        {
            switch (member)
            {
                case IFieldSymbol fieldSymbol:
                {
                    var initializer = GetInitializerSyntax(fieldSymbol);
                    if (initializer is null) break;

                    foreach (var parameter in GetParameterSymbols(initializer))
                        result[parameter] =
                            new FieldStorage(fieldSymbol.Name, fieldSymbol.Type, constructorContainingNamespace)
                            {
                                DefinitionExists = true
                            };
                    break;
                }
                case IPropertySymbol propertySymbol:
                {
                    var initializer = GetInitializerSyntax(propertySymbol);
                    if (initializer is null) break;

                    foreach (var parameter in GetParameterSymbols(initializer))
                        result[parameter] =
                            new PropertyStorage(propertySymbol.Name, propertySymbol.Type, constructorContainingNamespace)
                            {
                                DefinitionExists = true
                            };
                    break;
                }
            }
        }

        return;

        IEnumerable<IParameterSymbol> GetParameterSymbols(ExpressionSyntax initializer)
        {
            return from parameter in primaryConstructor.Parameters
                let isInitialized = IsInitializedFromParameter(initializer, parameter)
                where isInitialized
                select parameter;
        }
    }

    private bool IsInitializedFromParameter(ExpressionSyntax initializer, IParameterSymbol parameter)
    {
        // Check if initializer is a simple reference to the parameter
        if (initializer is not IdentifierNameSyntax identifier)
            return false;

        var symbolInfo = semanticModel.GetSymbolInfo(identifier);
        return SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, parameter);
    }

    private static ExpressionSyntax? GetInitializerSyntax(ISymbol symbol)
    {
        var declaringSyntax = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();

        return symbol switch
        {
            IFieldSymbol when declaringSyntax is VariableDeclaratorSyntax fieldDeclarator =>
                fieldDeclarator.Initializer?.Value,
            IPropertySymbol when declaringSyntax is PropertyDeclarationSyntax propertyDeclaration =>
                propertyDeclaration.Initializer?.Value,
            _ => null
        };
    }
}
