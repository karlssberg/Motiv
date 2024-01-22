namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Reasons;

/// <summary>
/// Represents a part of a fluent-builder interface that configures the reasons that are yielded when all elements
/// in a collection satisfy the underlying specification. This interface combines the functionalities of
/// IYieldAnyTrueReasons.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
public interface IYieldAllTrueReasons<TModel, TUnderlyingMetadata> :
    IYieldAnyTrueReasons<TModel, TUnderlyingMetadata>
{
    /// <summary>Registers a function that yields reasons when all of the underlying boolean results are true.</summary>
    /// <param name="trueBecause">A function that maps a collection of results to a collection of reasons.</param>
    /// <returns>The next set of builder operations.</returns>
    IYieldAnyTrueReasonsOrFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAllTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<string>> trueBecause);
}