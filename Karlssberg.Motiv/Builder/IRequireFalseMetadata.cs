namespace Karlssberg.Motiv.Builder;

/// <summary>Represents an interface for asking for a false reason in a specification.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
public interface IRequireFalseMetadata<TModel, TMetadata>
{
    /// <summary>Provide a human readable explanation for when the condition is false.</summary>
    /// <param name="whenFalse">A human readable explanation pf why the predicate returned false.</param>
    /// <returns>A specification base.</returns>
    IRequireActivationWithDescription<TModel, TMetadata> YieldWhenFalse(TMetadata whenFalse);

    /// <summary>Supply a function that when executed generates a human readable explanation for when the condition is false.</summary>
    /// <param name="whenFalse">
    ///     The function that evaluates the model and returns a human readable explanation of why the
    ///     predicate returned false.
    /// </param>
    /// <returns>A specification base.</returns>
    IRequireActivationWithDescription<TModel, TMetadata> YieldWhenFalse(Func<TModel, TMetadata> whenFalse);
    
    /// <summary>Supply a function that when executed generates a human readable explanation for when the condition is false.</summary>
    /// <param name="whenFalse">
    ///     The function that evaluates the model and returns a human readable explanation of why the
    ///     predicate returned false.
    /// </param>
    /// <returns>A specification base.</returns>
    IRequireActivationWithDescription<TModel, TMetadata> YieldWhenFalse(Func<TMetadata> whenFalse);
}

/// <summary>Represents an interface for specifying the behavior when a condition is false.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public interface IRequireFalseReason<TModel>
{
    /// <summary>Specifies the behavior when the condition is false.</summary>
    /// <param name="falseBecause">The metadata associated with the condition.</param>
    /// <returns>The specification with the specified metadata.</returns>
    IRequireActivation<TModel> YieldWhenFalse(string falseBecause);
    
    
    /// <summary>Supply a function that when executed generates a human readable explanation for when the condition is false.</summary>
    /// <param name="falseBecause">
    ///     The function that evaluates the model and returns a human readable explanation of why the
    ///     predicate returned false.
    /// </param>
    /// <returns>A specification base.</returns>
    IRequireActivation<TModel> YieldWhenFalse(Func<TModel, string> falseBecause);
    
    /// <summary>Supply a function that when executed generates a human readable explanation for when the condition is false.</summary>
    /// <param name="falseBecause">
    ///     The function that evaluates the model and returns a human readable explanation of why the
    ///     predicate returned false.
    /// </param>
    /// <returns>A specification base.</returns>
    IRequireActivation<TModel> YieldWhenFalse(Func<string> falseBecause);
}