namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;

/// <summary>
/// Represents a part of a fluent-builder interface that configures the reasons that are yielded when any elements
/// in a collection fail to satisfy the underlying specification.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
public interface IYieldAnyFalseReasons<TModel, TUnderlyingMetadata> :
    IHigherOrderSpecFactory<TModel, string>
{
    /// <summary>Registers a function that yields a reason when any of the boolean results are false.</summary>
    /// <param name="falseBecause">A function that receives the boolean results and returns the relevant reasons.</param>
    /// <returns>The next set of builder operations.</returns>
    IYieldAllFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAnyFalse(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<string>> falseBecause);
}