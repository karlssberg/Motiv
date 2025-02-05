using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.FluentModel.Steps;
using Motiv.Generator.FluentBuilder.Generation;

namespace Motiv.Generator.FluentBuilder.FluentModel.Methods;

public class MultiMethod : IFluentMethod
{
    private readonly Lazy<ImmutableArray<FluentTypeParameter>> _lazyTypeParameters;

    private readonly Lazy<ImmutableArray<FluentMethodParameter>> _lazyMethodParameters;

    public MultiMethod(
        string methodName,
        IParameterSymbol sourceParameterSymbol,
        IFluentReturn fluentReturn,
        INamespaceSymbol rootNamespace,
        IMethodSymbol parameterConverter,
        ImmutableArray<FluentMethodParameter> availableParameterFields)
    {
        _lazyMethodParameters = new Lazy<ImmutableArray<FluentMethodParameter>>(GetMethodParameters);
        _lazyTypeParameters = new Lazy<ImmutableArray<FluentTypeParameter>>(GetTypeParameters);

        MethodName = methodName;
        RootNamespace = rootNamespace;
        SourceParameter = sourceParameterSymbol;
        Return = fluentReturn;
        ParameterConverter = parameterConverter;
        AvailableParameterFields = availableParameterFields;
    }

    public IMethodSymbol ParameterConverter { get; }
    public string MethodName { get; }
    public ImmutableArray<FluentMethodParameter> MethodParameters => _lazyMethodParameters.Value;

    public IParameterSymbol SourceParameter { get; }

    public ImmutableArray<FluentMethodParameter> AvailableParameterFields { get; }

    public IFluentReturn Return { get; }

    public ImmutableArray<FluentTypeParameter> TypeParameters => _lazyTypeParameters.Value;
    public INamespaceSymbol RootNamespace { get; }

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

    private ImmutableArray<FluentTypeParameter> GetTypeParameters()
    {
        return
        [
            ..ParameterConverter.TypeArguments
                .OfType<ITypeParameterSymbol>()
                .Select(typeParameter => new FluentTypeParameter(typeParameter))
        ];
    }
}
