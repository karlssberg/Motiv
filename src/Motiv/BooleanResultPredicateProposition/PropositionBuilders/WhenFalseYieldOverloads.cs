using Motiv.Generator.Attributes;

namespace Motiv.BooleanResultPredicateProposition.PropositionBuilders;

internal class WhenFalseYieldOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<TMetadata>> WhenFalseYield<TModel, TMetadata>(Func<TModel, IEnumerable<TMetadata>> function)
    {
        return function;
    }

    [FluentMethodTemplate]
    internal static Func<TModel, TBooleanResult, IEnumerable<TNewMetadata>> WhenFalse<TModel, TBooleanResult, TNewMetadata>(Func<TModel, TBooleanResult, TNewMetadata> function)
    {
        return (model, result) => [function(model, result)];
    }

    [FluentMethodTemplate]
    internal static Func<TModel, TBooleanResult, IEnumerable<TNewMetadata>> WhenFalse<TModel, TBooleanResult, TNewMetadata>(TNewMetadata value)
    {
        return  (_, _) => [value];
    }
}
