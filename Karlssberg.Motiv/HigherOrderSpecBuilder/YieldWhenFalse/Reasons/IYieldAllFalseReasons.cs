namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;

/// <summary>
/// Represents a part of a fluent-builder interface that configures the reasons for when all the underlying
/// specifications are not satisfied.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
public interface IYieldAllFalseReasons<TModel, TUnderlyingMetadata> : IHigherOrderSpecFactory<TModel, string>
{
    /// <summary>Register a function that yields a reason for when all of the underlying boolean results are false. .</summary>
    /// <param name="falseBecause">A function that receives the boolean results and returns the relevant reasons.</param>
    /// <returns>The next set of builder operations.</returns>
    IHigherOrderSpecFactory<TModel, string> YieldWhenAllFalse(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<string>> falseBecause);
}