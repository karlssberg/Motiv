using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Reasons;

/// <summary>
/// Represents a part of a fluent-builder interface that configures the reasons that are yielded when any elements
/// in a collection satisfy the underlying specification.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
public interface IYieldAnyTrueReasons<TModel, TUnderlyingMetadata>
{
    /// <summary>Registers a function that yields reasons when any of the boolean results are true.</summary>
    /// <param name="trueBecause">A function that maps a collection of results to a collection of reasons.</param>
    /// <returns>The next set of builder operations.</returns>
    IYieldFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAnyTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<string>> trueBecause);
}