using Karlssberg.Motiv.SpecBuilder.Phase2;

namespace Karlssberg.Motiv.SpecBuilder.Phase1;

public interface IRequireTrueReason<TModel> 
{
    /// <summary>Sets the reason why the condition is true.</summary>
    /// <param name="trueBecause">The reason why the condition is true.</param>
    /// <returns>An instance of <see cref="IRequireFalseReason{TModel}" />.</returns>
    IRequireFalseReason<TModel> YieldWhenTrue(string trueBecause);
    
    /// <summary>Sets the reason why the condition is true.</summary>
    /// <param name="trueBecause">The reason why the condition is true.</param>
    /// <returns>An instance of <see cref="IRequireFalseReason{TModel}" />.</returns>
    IRequireFalseReasonWhenDescriptionUnresolved<TModel> YieldWhenTrue(Func<string> trueBecause);
    
    /// <summary>Sets the reason why the condition is true.</summary>
    /// <param name="trueBecause">The reason why the condition is true.</param>
    /// <returns>An instance of <see cref="IRequireFalseReason{TModel}" />.</returns>
    IRequireFalseReasonWhenDescriptionUnresolved<TModel> YieldWhenTrue(Func<TModel, string> trueBecause);
}