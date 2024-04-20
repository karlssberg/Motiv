namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for predicates. These methods convert predicates into propositions.
/// </summary>
public static class SpecExtensions
{
    internal static Func<TModel, BooleanResultBase<TMetadata>> ToBooleanResultPredicate<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec) =>
        spec.IsSatisfiedBy;
}