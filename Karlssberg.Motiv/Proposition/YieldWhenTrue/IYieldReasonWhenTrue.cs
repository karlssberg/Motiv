using Karlssberg.Motiv.Proposition.WhenFalse;

namespace Karlssberg.Motiv.Proposition.WhenTrue;

public interface IYieldReasonWhenTrue<TModel>
{
    /// <summary>Sets the reason why the condition is true.</summary>
    /// <param name="trueBecause">The human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="IYieldReasonWhenFalse{TModel}" />.</returns>
    IYieldReasonWhenFalse<TModel> WhenTrue(string trueBecause);


    /// <summary>Sets the reason why the condition is true.</summary>
    /// <param name="trueBecause">The human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="IYieldReasonWhenFalse{TModel}" />.</returns>
    IYieldReasonWithDescriptionUnresolvedWhenFalse<TModel> WhenTrue(Func<TModel, string> trueBecause);
}