namespace Karlssberg.Motiv.ChangeMetadataType.YieldWhenFalse;

/// <summary>Represents an interface for asking for a false reason in a specification.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public interface IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel, TUnderlyingMetadata> : IChangeReasonBuilder<TModel, TUnderlyingMetadata>
{
    /// <summary>Provide a human readable explanation for when the condition is false.</summary>
    /// <param name="falseBecause">A human readable explanation pf why the predicate returned false.</param>
    /// <returns>A specification base.</returns>
    SpecBase<TModel, string> WhenFalse(string falseBecause);

    /// <summary>Supply a function that when executed generates a human readable explanation for when the condition is false.</summary>
    /// <param name="falseBecause">
    ///     The function that evaluates the model and returns a human readable explanation of why the
    ///     predicate returned false.
    /// </param>
    /// <returns>A specification base.</returns>
    SpecBase<TModel, string> WhenFalse(Func<TModel, string> falseBecause);
}