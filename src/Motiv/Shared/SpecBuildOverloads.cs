using Motiv.Generator.Attributes;

namespace Motiv.Shared;

internal static class SpecBuildOverloads
{

    [FluentMethodTemplate]
    internal static SpecBase<TModel, TMetadata> Build<TModel, TMetadata>(SpecBase<TModel, TMetadata> spec) => spec;

    [FluentMethodTemplate]
    internal static SpecBase<TModel, TMetadata> Build<TModel, TMetadata>(Func<SpecBase<TModel, TMetadata>> specFactory) => specFactory();

    [FluentMethodTemplate]
    internal static SpecBase<TModel, string> Build<TModel>(SpecBase<TModel, string> spec) => spec;

    [FluentMethodTemplate]
    internal static SpecBase<TModel, string> Build<TModel>(Func<SpecBase<TModel, string>> specFactory) => specFactory();
}
