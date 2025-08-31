using Motiv.FluentFactory.Generator;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders.Overloads;

internal class WhenFalseYieldOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, TBooleanResult, IEnumerable<TMetadata>> WhenFalseYield<TModel, TMetadata, TBooleanResult>(Func<TModel, TBooleanResult, IEnumerable<TMetadata>> whenFalse)
    {
        return whenFalse;
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
