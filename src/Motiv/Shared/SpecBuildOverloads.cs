using Motiv.Generator.Attributes;

namespace Motiv.Shared;

internal static class SpecBuildOverloads
{

    [FluentMethodTemplate]
    internal static SpecBase<TModel, TMetadata> Build<TModel, TMetadata>(SpecBase<TModel, TMetadata> spec) =>
        spec.ThrowIfNull(nameof(spec));

    [FluentMethodTemplate]
    internal static SpecBase<TModel, TMetadata> Build<TModel, TMetadata>(Func<SpecBase<TModel, TMetadata>> specFactory) =>
        specFactory.ThrowIfNull(nameof(specFactory)).Invoke();
}
