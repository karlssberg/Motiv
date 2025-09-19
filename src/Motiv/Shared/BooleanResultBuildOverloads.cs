using Motiv.FluentFactory.Attributes;

namespace Motiv.Shared;

internal static class BooleanResultBuildOverloads
{

    [FluentMethodTemplate]
    internal static Func<TModel, BooleanResultBase<TMetadata>> Build<TModel, TMetadata>(Func<TModel, BooleanResultBase<TMetadata>> resultFactory) =>
        resultFactory.ThrowIfNull(nameof(resultFactory));
}
