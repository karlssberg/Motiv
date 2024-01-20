using Karlssberg.Motiv.SpecBuilder;
using Karlssberg.Motiv.SpecBuilder.Phase1;

namespace Karlssberg.Motiv;

public static class PredicateExtensions
{
    /// <summary>Converts a predicate function into a <see cref="SpecBuilder{TModel}" /> instance.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="predicate">The predicate function.</param>
    /// <returns>A new instance of <see cref="SpecBuilder{TModel}" /> initialized with the specified predicate.</returns>
    public static IRequireTrueReason<TModel> ToSpec<TModel>(this Func<TModel, bool> predicate) => new SpecBuilder<TModel>(predicate);
}