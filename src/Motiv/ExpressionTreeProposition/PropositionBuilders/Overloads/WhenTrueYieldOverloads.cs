using Converj.Attributes;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders.Overloads;

internal class WhenTrueYieldOverloads
{
    [FluentMethodTemplate]
    internal static Func<TModel, TResult, IEnumerable<TMetadata>> WhenTrueYield<TModel, TMetadata, TResult>(Func<TModel, TResult, IEnumerable<TMetadata>> whenTrue)
    {
        return whenTrue;
    }
}
