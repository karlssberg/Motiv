using Microsoft.CodeAnalysis;
using Motiv.Generator.Model.Methods;

namespace Motiv.Generator.Model.Steps;

public interface IFluentStep : IFluentReturn
{
    string Name { get; }

    string FullName { get; }

    IList<IFluentMethod> FluentMethods { get; }

    Accessibility Accessibility { get; }

    TypeKind TypeKind { get; }

    bool IsRecord { get; }
}
