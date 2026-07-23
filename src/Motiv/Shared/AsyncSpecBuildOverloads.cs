using Converj.Attributes;

namespace Motiv.Shared;

/// <summary>
/// Fluent method templates for <c>Spec.Build(asyncSpec)</c>, mirroring <see cref="SpecBuildOverloads" />.
/// The <c>AsyncSpecBase&lt;TModel, string&gt;</c> specialisations exist because
/// <see cref="Motiv.DecoratorProposition.PropositionBuilders.AsyncSpec.AsyncPropositionFactory{TModel,TReplacementMetadata}" />
/// declares that exact constructor parameter type (as the sync metadata
/// <see cref="Motiv.DecoratorProposition.PropositionBuilders.Spec.PropositionFactory{TModel,TReplacementMetadata}" />
/// does) — without them the generator emits a detached string-specialised step that severs the minimal and
/// explanation chains from <c>Spec.Build(asyncStringSpec)</c>.
/// </summary>
internal static class AsyncSpecBuildOverloads
{
    [FluentMethodTemplate]
    internal static AsyncSpecBase<TModel, TMetadata> Build<TModel, TMetadata>(AsyncSpecBase<TModel, TMetadata> spec) =>
        spec.ThrowIfNull(nameof(spec));

    [FluentMethodTemplate]
    internal static AsyncSpecBase<TModel, TMetadata> Build<TModel, TMetadata>(Func<AsyncSpecBase<TModel, TMetadata>> specFactory) =>
        specFactory.ThrowIfNull(nameof(specFactory)).Invoke();

    [FluentMethodTemplate]
    internal static AsyncSpecBase<TModel, string> Build<TModel>(AsyncSpecBase<TModel, string> spec) =>
        spec.ThrowIfNull(nameof(spec));

    [FluentMethodTemplate]
    internal static AsyncSpecBase<TModel, string> Build<TModel>(Func<AsyncSpecBase<TModel, string>> specFactory) =>
        specFactory.ThrowIfNull(nameof(specFactory)).Invoke();
}
