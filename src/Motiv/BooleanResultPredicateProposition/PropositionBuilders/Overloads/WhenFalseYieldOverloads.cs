using Motiv.Generator;

namespace Motiv.BooleanResultPredicateProposition.PropositionBuilders.Overloads;

internal class WhenFalseYieldOverloads
{

    [FluentMethodTemplate]
    internal static Func<TModel, TResult, IEnumerable<TMetadata>> WhenFalseYield<TModel, TMetadata, TResult>(Func<TModel, TResult, IEnumerable<TMetadata>> whenFalse)
    {
        return whenFalse;
    }

    [FluentMethodTemplate]
    internal static Func<TModel, TResult, IEnumerable<TMetadata>> WhenFalseYield<TModel, TMetadata, TResult>(Func<TModel, IEnumerable<TMetadata>> whenFalse)
    {
        return (model, _) => whenFalse(model);
    }

    [FluentMethodTemplate]
    internal static Func<TModel, TBooleanResult, IEnumerable<TNewMetadata>> WhenFalse<TModel, TBooleanResult, TNewMetadata>(Func<TModel, TBooleanResult, TNewMetadata> whenFalse)
    {
        return whenFalse.ToEnumerableReturn();
    }

    [FluentMethodTemplate]
    internal static Func<TModel, TBooleanResult, IEnumerable<TNewMetadata>> WhenFalse<TModel, TBooleanResult, TNewMetadata>(Func<TModel, TNewMetadata> whenFalse)
    {
        return (model, _) => whenFalse(model).ToEnumerable();
    }

    [FluentMethodTemplate]
    internal static Func<TModel, TBooleanResult, IEnumerable<TNewMetadata>> WhenFalse<TModel, TBooleanResult, TNewMetadata>(TNewMetadata whenFalse)
    {
        return (_, _) => whenFalse.ToEnumerable();
    }
}
