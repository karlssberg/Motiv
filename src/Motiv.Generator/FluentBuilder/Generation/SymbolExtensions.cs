using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.Generator.FluentBuilder.Generation;

public static class SymbolExtensions
{
    private static readonly SymbolDisplayFormat TypeNameOnlyFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
    );

    public static bool IsOpenGenericType(this ITypeSymbol type)
    {
        return type switch
        {
            ITypeParameterSymbol => true,
            INamedTypeSymbol namedType => ContainsUnboundGenericTypes(namedType),
            _ => false
        };
    }

    private static bool ContainsUnboundGenericTypes(INamedTypeSymbol namedType)
    {
        return namedType.TypeArguments.Any(t => t switch
               {
                   ITypeParameterSymbol => true,
                   INamedTypeSymbol { IsGenericType: true } typeSymbol => typeSymbol.IsOpenGenericType(),
                   _ => false
               }) ||
               // Also check if this is a generic type definition itself
               namedType.IsUnboundGenericType;
    }

    public static IEnumerable<TypeParameterSyntax> GetGenericTypeParameterSyntaxList(this IEnumerable<ITypeSymbol> types)
    {
        return types.GetGenericTypeParameters()
            .Select(ToTypeParameterSyntax);
    }

    public static IEnumerable<TypeParameterSyntax> GetGenericTypeParameterSyntaxList(this ITypeSymbol type)
    {
        return new[] { type }.GetGenericTypeParameterSyntaxList();
    }

    public static IEnumerable<ITypeParameterSymbol> GetGenericTypeParameters(this IEnumerable<ITypeSymbol> type)
    {
        return type
            .SelectMany(symbol => symbol.GetGenericTypeParameters())
            .DistinctBy(symbol => symbol.ToDisplayString());
    }

    public static TypeParameterSyntax ToTypeParameterSyntax(this ITypeParameterSymbol typeParameter)
    {
        return SyntaxFactory.TypeParameter(SyntaxFactory.Identifier(typeParameter.Name));
    }

    public static IEnumerable<ITypeParameterSymbol> GetGenericTypeParameters(this ITypeSymbol type)
    {
        return type switch
        {
            ITypeParameterSymbol typeParameter => [typeParameter],
            INamedTypeSymbol namedType => namedType.TypeArguments
                .SelectMany(typeArg => typeArg.GetGenericTypeParameters())
                .Distinct<ITypeParameterSymbol>(SymbolEqualityComparer.Default),
            _ => []
        };
    }

    public static IEnumerable<ITypeParameterSymbol> GetGenericTypeArguments(this ITypeSymbol type)
    {
        return GenericTypeArgumentsInternal(type).DistinctBy(type => type.Name);
    }

    private static IEnumerable<ITypeParameterSymbol> GenericTypeArgumentsInternal(ITypeSymbol type)
    {
        return type switch
        {
            ITypeParameterSymbol typeParameter => [typeParameter],
            INamedTypeSymbol namedType => namedType.TypeArguments
                .SelectMany(t => t.GetGenericTypeArguments()),
            _ => []
        };
    }

    public static bool IsAssignable(this Compilation compilation, ITypeSymbol? source, ITypeSymbol? destination)
    {
        if (source is null || destination is null)
            return false;

        // If they're exactly the same type, return true
        if (SymbolEqualityComparer.Default.Equals(source, destination))
            return true;

        switch (source, destination)
        {
            case (_, ITypeParameterSymbol destinationTypeParam):
            {
                // Check if source satisfies all target's constraints
                if (destinationTypeParam.ConstraintTypes
                    .Select(constraint => compilation.ClassifyCommonConversion(source, constraint))
                    .Any(paramConversion => paramConversion is { Exists: false }))
                {
                    return false;
                }

                if (destinationTypeParam.HasValueTypeConstraint && !source.IsValueType)
                    return false;

                if (destinationTypeParam.HasReferenceTypeConstraint && !source.IsReferenceType)
                    return false;

                if (destinationTypeParam.HasConstructorConstraint && !source.IsReferenceType)
                    return false;

                return true;
            }
            case (ITypeParameterSymbol sourceTypeParam, _):
            {
                if (sourceTypeParam.ConstraintTypes
                    .Select(constraint => compilation.ClassifyCommonConversion(constraint, destination))
                    .Any(typeConversion => typeConversion is { Exists: true, IsImplicit: true }))
                {
                    return true;
                }

                // Check special constraints against target
                if (sourceTypeParam.HasValueTypeConstraint && !destination.IsValueType)
                    return false;

                return !sourceTypeParam.HasReferenceTypeConstraint || destination.IsReferenceType;
            }
            case (INamedTypeSymbol sourceNamedType, INamedTypeSymbol destinationNamedType):
            {
                // Check if they have the same number of type arguments
                if (sourceNamedType.TypeArguments.Length != destinationNamedType.TypeArguments.Length)
                    return false;
                // All other parameters are contravariant
                for (var i = 0; i < sourceNamedType.TypeArguments.Length; i++)
                {
                    var paramTypeArg = sourceNamedType.TypeArguments[i];
                    var argTypeArg = destinationNamedType.TypeArguments[i];

                    if (!compilation.IsAssignable(paramTypeArg, argTypeArg))
                        return false;
                }
                return true;
            }
            default:
                // Fall back to checking conversion
                var conversion = compilation.ClassifyConversion(source, destination);
                return conversion is { Exists: true, IsImplicit: true };
        }
    }

    public static IEnumerable<ITypeParameterSymbol> Union(
       this IEnumerable<ITypeParameterSymbol> first,
       IEnumerable<ITypeParameterSymbol> second)
   {
       return first
           .Union<ITypeParameterSymbol>(second, SymbolEqualityComparer.IncludeNullability)
           .OrderBy(symbol => symbol.Name);
   }


    public static IEnumerable<ITypeParameterSymbol> Except(
       this IEnumerable<ITypeParameterSymbol> collection,
       IEnumerable<ITypeParameterSymbol> exclusions)
   {
       var exclusionSet = new HashSet<string>(exclusions.Select(parameter => parameter.ToDisplayString()));

       foreach (var item in collection)
       {
          if (!exclusionSet.Contains(item.ToDisplayString()))
            yield return item;
       }
   }

   public static IEnumerable<SyntaxKind> AccessibilityToSyntaxKind(this Accessibility accessibility) =>
       accessibility switch
       {
           Accessibility.Public => [SyntaxKind.PublicKeyword],
           Accessibility.Private => [SyntaxKind.PrivateKeyword],
           Accessibility.Protected => [SyntaxKind.ProtectedKeyword],
           Accessibility.Internal => [SyntaxKind.InternalKeyword],
           Accessibility.ProtectedOrInternal => [SyntaxKind.ProtectedKeyword, SyntaxKind.InternalKeyword], // Note: This will need both protected and internal
           Accessibility.ProtectedAndInternal => [SyntaxKind.PrivateKeyword, SyntaxKind.ProtectedKeyword],
           _ => [SyntaxKind.None]
       };

   private static readonly SymbolDisplayFormat NameFormat = new(
         typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
         genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
         miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );

    public static string ToUnqualifiedDisplayString(this ITypeSymbol typeSymbol) =>
       typeSymbol.ToDisplayString(TypeNameOnlyFormat);

   public static string ToDynamicDisplayString(this ITypeSymbol typeSymbol, INamespaceSymbol currentNamespace)
   {
       switch (typeSymbol)
       {
           case null:
               return string.Empty;
           // Handle named type symbols (regular classes, structs, interfaces)
           case INamedTypeSymbol namedType:
           {
               // Get the base name without namespace
               var baseName = GetBaseTypeName(namedType, currentNamespace);

               // If it's not a generic type, return the base name
               if (!namedType.IsGenericType)
                   return baseName;

               // Handle generic type parameters
               var typeArguments = namedType.TypeArguments;
               if (!typeArguments.Any())
                   return baseName;

               // Process each type argument recursively
               var processedTypeArgs = typeArguments.Select(arg => arg.ToDynamicDisplayString(currentNamespace));

               // Combine the base name with processed type arguments
               return $"{baseName.Split('<')[0]}<{string.Join(", ", processedTypeArgs)}>";
           }
           // Handle array types
           case IArrayTypeSymbol arrayType:
               return $"{arrayType.ElementType.ToDynamicDisplayString(currentNamespace)}[]";
           default:
               // Handle other type symbols (like type parameters)
               return typeSymbol.Name;
       }
   }

   private static string GetBaseTypeName(INamedTypeSymbol typeSymbol, INamespaceSymbol currentNamespace)
   {
       var fullName = typeSymbol.ContainingType != null
           ? $"{typeSymbol.ContainingType.ToDynamicDisplayString(currentNamespace)}.{typeSymbol.Name}"
           : typeSymbol.ToDisplayString();

       var namespaceName = currentNamespace.ToDisplayString();

       // Remove namespace prefix if it matches current namespace
       return typeSymbol.ToDisplayString().StartsWith(namespaceName)
           ? fullName.Substring(namespaceName.Length).TrimStart('.')
           : fullName;
   }
}
