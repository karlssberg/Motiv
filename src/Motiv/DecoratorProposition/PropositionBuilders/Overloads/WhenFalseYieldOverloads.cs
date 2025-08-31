using Motiv.FluentFactory.Generator;

namespace Motiv.DecoratorProposition.PropositionBuilders.Overloads;

internal class WhenFalseYieldOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, TResult,  IEnumerable<TMetadata>> WhenFalseYield<TModel, TMetadata, TResult>(Func<TModel, TResult,  IEnumerable<TMetadata>> whenFalse)
    {
        return whenFalse;
    }

    [FluentMethodTemplate]
    internal static Func<TModel, TResult, IEnumerable<TMetadata>> WhenFalse<TModel, TMetadata, TResult>(Func<TModel, TResult, TMetadata> whenFalse)
    {
        return whenFalse.ToEnumerableReturn();
    }

    [FluentMethodTemplate]
    internal static Func<TModel, TResult, IEnumerable<TMetadata>> WhenFalse<TModel, TMetadata, TResult>(Func<TModel, TMetadata> whenFalse)
    {
        return (model, _) => whenFalse(model).ToEnumerable();
    }

    [FluentMethodTemplate]
    internal static Func<TModel, TResult, IEnumerable<TMetadata>> WhenFalse<TModel, TMetadata, TResult>(TMetadata whenFalse)
    {
        return (_, _) => whenFalse.ToEnumerable();
    }
}
