
using Karlssberg.Motiv.ChangeMetadata.YieldWhenFalse;

namespace Karlssberg.Motiv.ChangeMetadata.YieldWhenTrue;

public interface IYieldReasonWhenTrue<TModel>
{
    /// <summary>Sets the reason why the condition is true.</summary>
    /// <param name="trueBecause">The human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="IYieldReasonWhenFalse{TModel}" />.</returns>
    IYieldReasonWhenFalse<TModel> YieldWhenTrue(string trueBecause);

    /// <summary>Sets the reason why the condition is true.</summary>
    /// <param name="trueBecause">The human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="IYieldReasonWhenFalse{TModel}" />.</returns>
    IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel> YieldWhenTrue(Func<string> trueBecause);

    /// <summary>Sets the reason why the condition is true.</summary>
    /// <param name="trueBecause">The human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="IYieldReasonWhenFalse{TModel}" />.</returns>
    IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel> YieldWhenTrue(Func<TModel, string> trueBecause);
}