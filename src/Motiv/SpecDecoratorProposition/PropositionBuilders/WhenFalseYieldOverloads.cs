using Motiv.Generator.Attributes;

namespace Motiv.SpecDecoratorProposition.PropositionBuilders;

internal class WhenFalseYieldOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, IEnumerable<TMetadata>> WhenFalseYield<TModel, TMetadata>(Func<TModel, IEnumerable<TMetadata>> function)
    {
        return function;
    }
}
