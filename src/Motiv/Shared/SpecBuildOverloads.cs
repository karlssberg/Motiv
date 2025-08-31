using Motiv.FluentFactory.Generator;

namespace Motiv.Shared;

internal static class SpecBuildOverloads
{

    [FluentMethodTemplate]
    internal static SpecBase<TModel, TMetadata> Build<TModel, TMetadata>(SpecBase<TModel, TMetadata> spec) =>
        spec.ThrowIfNull(nameof(spec));

    [FluentMethodTemplate]
    internal static SpecBase<TModel, TMetadata> Build<TModel, TMetadata>(Func<SpecBase<TModel, TMetadata>> specFactory) =>
        specFactory.ThrowIfNull(nameof(specFactory)).Invoke();

    [FluentMethodTemplate]
    internal static SpecBase<TModel, string> Build<TModel>(SpecBase<TModel, string> spec) =>
        spec.ThrowIfNull(nameof(spec));

    [FluentMethodTemplate]
    internal static SpecBase<TModel, string> Build<TModel>(Func<SpecBase<TModel, string>> specFactory) =>
        specFactory.ThrowIfNull(nameof(specFactory)).Invoke();
}
