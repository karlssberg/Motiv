using Microsoft.CodeAnalysis;

namespace Motiv.Generator.Generation;

public static class GenericAnalysis
{
    public static Dictionary<ITypeParameterSymbol, ITypeSymbol> GetGenericParameterMappings(
        ITypeSymbol openType,
        ITypeSymbol closedType)
    {
        var result = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
        PopulateGenericParameterMappings(openType, closedType, result);
        return result;
    }

    private static void PopulateGenericParameterMappings(
        ITypeSymbol? openType,
        ITypeSymbol? closedType,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> mappings)
    {
        // Base cases
        if (openType == null || closedType == null)
            return;

        switch (openType)
        {
            // Handle type parameters - this is where the actual mapping happens
            case ITypeParameterSymbol typeParam when closedType is not ITypeParameterSymbol:
                mappings[typeParam] = closedType;
                return;
            // Handle array types
            case IArrayTypeSymbol openArray when closedType is IArrayTypeSymbol closedArray:
                PopulateGenericParameterMappings(openArray.ElementType, closedArray.ElementType, mappings);
                return;
            // Handle named types (classes, interfaces, delegates, etc.)
            case INamedTypeSymbol openNamed when closedType is INamedTypeSymbol closedNamed:
            {
                // Verify these are variations of the same type
                if (!SymbolEqualityComparer.Default.Equals(openNamed.OriginalDefinition, closedNamed.OriginalDefinition))
                    return;

                // Process type arguments recursively
                var openTypeArgs = openNamed.TypeArguments;
                var closedTypeArgs = closedNamed.TypeArguments;

                if (openTypeArgs.Length == closedTypeArgs.Length)
                {
                    for (var i = 0; i < openTypeArgs.Length; i++)
                    {
                        PopulateGenericParameterMappings(openTypeArgs[i], closedTypeArgs[i], mappings);
                    }
                }

                // Special handling for delegates (like Func<>)
                if (openNamed.DelegateInvokeMethod != null && closedNamed.DelegateInvokeMethod != null)
                {
                    var openMethod = openNamed.DelegateInvokeMethod;
                    var closedMethod = closedNamed.DelegateInvokeMethod;

                    // Process return type
                    PopulateGenericParameterMappings(openMethod.ReturnType, closedMethod.ReturnType, mappings);

                    // Process parameters
                    var openParams = openMethod.Parameters;
                    var closedParams = closedMethod.Parameters;

                    if (openParams.Length == closedParams.Length)
                    {
                        for (var i = 0; i < openParams.Length; i++)
                        {
                            PopulateGenericParameterMappings(openParams[i].Type, closedParams[i].Type, mappings);
                        }
                    }
                }

                break;
            }
        }
    }
}
