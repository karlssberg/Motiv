using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.Model.Methods;
using Motiv.Generator.FluentBuilder.Model.Storage;

namespace Motiv.Generator.FluentBuilder.Model.Steps;

public interface IFluentStep : IFluentReturn
{
    string Name { get; }

    string FullName { get; }

    IList<IFluentMethod> FluentMethods { get; }

    Accessibility Accessibility { get; }

    TypeKind TypeKind { get; }

    bool IsRecord { get; }
}
