using Karlssberg.Motiv.ElseIf;
using Karlssberg.Motiv.Proposition;
using Karlssberg.Motiv.Proposition.YieldWhenTrue;

namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for predicates. These methods convert predicates into specifications.
/// </summary>
public static class SpecExtensions
{
    /// <summary>
    /// Converts a predicate function into a SpecBuilder instance.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="predicate">The predicate function.</param>
    /// <returns>A new instance of SpecBuilder initialized with the specified predicate.</returns>
    public static IYieldReasonWhenTrue<TModel> ToSpec<TModel>(this Func<TModel, bool> predicate) =>
        new SpecBuilder<TModel>(predicate);
    
    public static SpecBase<TModel, TMetadata> ElseIf<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> antecedent,
        SpecBase<TModel, TMetadata> consequent)
    {
        return new ElseIfSpec<TModel, TMetadata>(antecedent, consequent);
    }
}