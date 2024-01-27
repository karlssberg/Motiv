using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Reasons;

/// <summary>
/// Represents a part of a fluent-builder interface that configures the reasons that are yielded when either any
/// elements in a collection satisfy the underlying specification or none do. This interface combines the functionalities
/// of IYieldAnyTrueReasons and IYieldAnyFalseReasons.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
public interface IYieldReasonsWhenAnyTrueOrFalse<TModel, TUnderlyingMetadata> :
    IYieldReasonsWhenAnyTrue<TModel, TUnderlyingMetadata>,
    IYieldReasonsWhenAnyFalse<TModel, TUnderlyingMetadata>
{
}