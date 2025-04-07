using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentFactory.Model.Methods;

namespace Motiv.Generator.FluentFactory.Model.Steps;

public interface IFluentStep : IFluentReturn
{
    string Name { get; }

    string FullName { get; }

    IList<IFluentMethod> FluentMethods { get; }

    Accessibility Accessibility { get; }

    TypeKind TypeKind { get; }

    bool IsRecord { get; }
}
