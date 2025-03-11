using Motiv.Generator.Attributes;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders;

internal class WhenTrueYieldOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<TMetadata>> WhenTrueYield<TModel, TMetadata>(Func<TModel, IEnumerable<TMetadata>> function)
    {
        return function;
    }

    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<string>> WhenTrueYield<TModel>(Func<TModel, IEnumerable<string>> function)
    {
        return function;
    }
}
