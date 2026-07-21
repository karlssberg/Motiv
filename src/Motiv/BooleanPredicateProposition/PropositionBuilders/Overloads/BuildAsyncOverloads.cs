using Converj.Attributes;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders.Overloads;

internal class BuildAsyncOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, CancellationToken, ValueTask<bool>> BuildAsync<TModel>(
        Func<TModel, CancellationToken, ValueTask<bool>> predicate)
    {
        return predicate;
    }

    [FluentMethodTemplate]
    internal static Func<TModel, CancellationToken, ValueTask<bool>> BuildAsync<TModel>(
        Func<TModel, ValueTask<bool>> predicate)
    {
        return (model, _) => predicate(model);
    }
}
