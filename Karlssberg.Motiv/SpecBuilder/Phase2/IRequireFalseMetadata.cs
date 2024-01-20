using Karlssberg.Motiv.SpecBuilder.Phase3;

namespace Karlssberg.Motiv.SpecBuilder.Phase2;

/// <summary>Represents an interface for asking for a false reason in a specification.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
public interface IRequireFalseMetadata<TModel, TMetadata>
{
    /// <summary>Provide a human readable explanation for when the condition is false.</summary>
    /// <param name="whenFalse">New metadata for when the result is false.</param>
    /// <returns>A specification base.</returns>
    IRequireActivationWithDescription<TModel, TMetadata> YieldWhenFalse(TMetadata whenFalse);

    /// <summary>Supply a function that when executed generates a human readable explanation for when the condition is false.</summary>
    /// <param name="whenFalse">The function that evaluates the model and returns new metadata when the result is false.</param>
    /// <returns>A specification base.</returns>
    IRequireActivationWithDescription<TModel, TMetadata> YieldWhenFalse(Func<TModel, TMetadata> whenFalse);

    /// <summary>Supply a function that when executed generates a human readable explanation for when the condition is false.</summary>
    /// <param name="whenFalse">The function that evaluates the model and returns new metadata when the result is false.</param>
    /// <returns>A specification base.</returns>
    IRequireActivationWithDescription<TModel, TMetadata> YieldWhenFalse(Func<TMetadata> whenFalse);
}