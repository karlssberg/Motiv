using Karlssberg.Motiv.BooleanPredicateProposition.PropositionBuilders;

namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for predicates. These methods convert predicates into propositions.
/// </summary>
public static class SpecExtensions
{
    /// <summary>
    /// Converts a predicate function into a SpecBuilder instance.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="predicate">The predicate function.</param>
    /// <returns>A new instance of SpecBuilder initialized with the specified predicate.</returns>
    public static BooleanPredicatePropositionBuilder<TModel> ToSpec<TModel>(this Func<TModel, bool> predicate) =>
        new (predicate);
    
    internal static Func<TModel, BooleanResultBase<TMetadata>> ToBooleanResultPredicate<TModel, TMetadata>(
        this Func<TModel, SpecBase<TModel, TMetadata>> specFactory) =>
        model => specFactory(model).IsSatisfiedBy(model);
    
    
    internal static Func<TModel, BooleanResultBase<TMetadata>> ToBooleanResultPredicate<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec) =>
        spec.IsSatisfiedBy;
}