using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.FluentModel.Steps;
using Motiv.Generator.FluentBuilder.Generation;

namespace Motiv.Generator.FluentBuilder.FluentModel.Methods;

public class RegularMethod : IFluentMethod
{
    public string MethodName { get; }

    public ImmutableArray<FluentMethodParameter> MethodParameters { get; }

    public IParameterSymbol SourceParameter { get; }

    public ImmutableArray<FluentMethodParameter> AvailableParameterFields { get; }

    public IFluentReturn Return { get; }


    private readonly Lazy<ImmutableArray<FluentTypeParameter>> _typeParameters;

    public RegularMethod(
        string methodName,
        IParameterSymbol sourceParameterSymbol,
        IFluentReturn fluentReturn,
        INamespaceSymbol rootNamespace,
        ImmutableArray<FluentMethodParameter> availableParameterFields)
    {
        MethodName = methodName;
        SourceParameter = sourceParameterSymbol;
        MethodParameters = [new FluentMethodParameter(sourceParameterSymbol, methodName)];
        RootNamespace = rootNamespace;
        AvailableParameterFields = availableParameterFields;
        Return = fluentReturn;
        _typeParameters = new Lazy<ImmutableArray<FluentTypeParameter>>(GetTypeParameters);
    }

    public ImmutableArray<FluentTypeParameter> TypeParameters => _typeParameters.Value;

    public INamespaceSymbol RootNamespace { get; }

    private ImmutableArray<FluentTypeParameter> GetTypeParameters()
    {
        return
        [
            ..SourceParameter.Type
                .GetGenericTypeParameters()
                .Select(genericTypeParameter => new FluentTypeParameter(genericTypeParameter))
        ];
    }
}
