using Motiv.Generator.Attributes;

namespace Motiv.Shared;

internal static class PolicyResultBuildOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, PolicyResultBase<TMetadata>> Build<TModel, TMetadata>(Func<TModel, PolicyResultBase<TMetadata>> resultFactory) =>
        resultFactory.ThrowIfNull(nameof(resultFactory));

    [FluentMethodTemplate]
    internal static Func<TModel, PolicyResultBase<string>> Build<TModel>(Func<TModel, PolicyResultBase<string>> resultFactory) =>
        resultFactory.ThrowIfNull(nameof(resultFactory));
}
