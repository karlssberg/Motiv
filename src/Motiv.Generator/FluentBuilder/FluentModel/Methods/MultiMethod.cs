using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.FluentModel.Steps;
using Motiv.Generator.FluentBuilder.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Motiv.Generator.FluentBuilder.FluentModel.Methods;

public class MultiMethod : IFluentMethod
{
    public string MethodName { get; }
    public ImmutableArray<FluentMethodParameter> MethodParameters => GetMethodParameters();

    public IParameterSymbol SourceParameter { get; }

    public ImmutableArray<FluentMethodParameter> AvailableParameterFields { get; }

    public IFluentReturn Return { get; }

    public IMethodSymbol ParameterConverter { get; }

    private readonly Lazy<ImmutableArray<FluentTypeParameter>> _typeParameters;

    public MultiMethod(
        string methodName,
        IParameterSymbol sourceParameterSymbol,
        IFluentReturn fluentReturn,
        INamespaceSymbol rootNamespace,
        IMethodSymbol parameterConverter,
        ImmutableArray<FluentMethodParameter> availableParameterFields)
    {
        _typeParameters = new Lazy<ImmutableArray<FluentTypeParameter>>(GetTypeParameters);

        MethodName = methodName;
        RootNamespace = rootNamespace;
        SourceParameter = sourceParameterSymbol;
        Return = fluentReturn;
        ParameterConverter = parameterConverter;
        AvailableParameterFields = availableParameterFields;
    }

    private ImmutableArray<FluentMethodParameter> GetMethodParameters()
    {
        return [..ParameterConverter.Parameters.Select(p => new FluentMethodParameter(p, MethodName))];

        // var typeArguments = _typeParameters.Value.Select(p => p.TypeParameterSymbol);
        // var constructedMethodSymbol = ParameterConverter.OriginalDefinition.Construct(typeArguments.ToArray<ITypeSymbol>());
        // return
        // [
        //     ..constructedMethodSymbol.Parameters.Select(p => p.Type is INamedTypeSymbol namedTypeSymbol
        //         ? new FluentMethodParameter(Symbol, MethodName, namedTypeSymbol)
        //         : new FluentMethodParameter(p, MethodName))
        // ];
    }

    public ImmutableArray<FluentTypeParameter> TypeParameters => _typeParameters.Value;
    public INamespaceSymbol RootNamespace { get; }

    private ImmutableArray<FluentTypeParameter> GetTypeParameters()
    {
        var parameterConverterTypeArguments = ParameterConverter.TypeArguments
            .OfType<ITypeParameterSymbol>()
            .Select(typeParameter => new FluentTypeParameter(typeParameter));

        var sourceParameterGenericParameters = SourceParameter.Type
            .GetGenericTypeParameters()
            .Select(typeParameter => new FluentTypeParameter(typeParameter));

        return
        [
            ..sourceParameterGenericParameters.Union(parameterConverterTypeArguments)
        ];
    }
}
