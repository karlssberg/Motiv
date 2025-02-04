using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.FluentModel.Methods;

namespace Motiv.Generator.FluentBuilder.FluentModel.Steps;

public interface IFluentStep : IFluentReturn
{
    string Name { get; }

    string FullName { get; }
    IList<IFluentMethod> FluentMethods { get; }
    Accessibility Accessibility { get; }
    TypeKind TypeKind { get; }
    bool IsRecord { get; }
    IReadOnlyDictionary<IParameterSymbol, FluentParameterResolution> ParameterStoreMembers { get; }
}
