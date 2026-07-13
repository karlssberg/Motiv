using Converj.Attributes;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders.Overloads;

internal class BuildAsyncOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, CancellationToken, Task<bool>> BuildAsync<TModel>(
        Func<TModel, CancellationToken, Task<bool>> predicate)
    {
        return predicate;
    }

    [FluentMethodTemplate]
    internal static Func<TModel, CancellationToken, Task<bool>> BuildAsync<TModel>(
        Func<TModel, Task<bool>> predicate)
    {
        return (model, _) => predicate(model);
    }
}
