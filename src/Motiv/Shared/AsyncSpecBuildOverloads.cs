using Converj.Attributes;

namespace Motiv.Shared;

/// <summary>
/// Fluent method templates for <c>Spec.Build(asyncSpec)</c>. Unlike <see cref="SpecBuildOverloads" />, there are
/// no <c>AsyncSpecBase&lt;TModel, string&gt;</c> specialisations: the generator only substitutes the factory's
/// <c>TMetadata</c> with <c>string</c> when a sharing factory declares that exact constructor parameter type
/// (as the sync explanation factories do). Until such an async factory exists, generic inference on the
/// <c>TMetadata</c> templates already resolves <c>TMetadata = string</c> for explanation specs.
/// </summary>
internal static class AsyncSpecBuildOverloads
{
    [FluentMethodTemplate]
    internal static AsyncSpecBase<TModel, TMetadata> Build<TModel, TMetadata>(AsyncSpecBase<TModel, TMetadata> spec) =>
        spec.ThrowIfNull(nameof(spec));

    [FluentMethodTemplate]
    internal static AsyncSpecBase<TModel, TMetadata> Build<TModel, TMetadata>(Func<AsyncSpecBase<TModel, TMetadata>> specFactory) =>
        specFactory.ThrowIfNull(nameof(specFactory)).Invoke();
}
