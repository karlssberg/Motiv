using Converj.Attributes;

namespace Motiv.Shared;

internal static class SpecBuildOverloads
{

    [FluentMethodTemplate]
    internal static SpecBase<TModel, TMetadata> Build<TModel, TMetadata>(SpecBase<TModel, TMetadata> spec) =>
        spec.ThrowIfNull();

    [FluentMethodTemplate]
    internal static SpecBase<TModel, TMetadata> Build<TModel, TMetadata>(Func<SpecBase<TModel, TMetadata>> specFactory) =>
        specFactory.ThrowIfNull().Invoke();

    [FluentMethodTemplate]
    internal static SpecBase<TModel, string> Build<TModel>(SpecBase<TModel, string> spec) =>
        spec.ThrowIfNull();

    [FluentMethodTemplate]
    internal static SpecBase<TModel, string> Build<TModel>(Func<SpecBase<TModel, string>> specFactory) =>
        specFactory.ThrowIfNull().Invoke();
}
