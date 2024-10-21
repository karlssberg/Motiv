using System.Linq.Expressions;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition;

internal class ExpressionTreeDescription<TModel, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    ISpecDescription? underlyingDescription = null)
    : IExpressionDescription<TModel>
{
    public string Statement { get; } = expression.Body.Humanize();

    public string Detailed => string.Join(Environment.NewLine, GetDetailsAsLines());

    public IEnumerable<string> GetDetailsAsLines()
    {
        yield return Statement;
        if (underlyingDescription is null)
            yield break;

        foreach (var line in underlyingDescription.GetDetailsAsLines())
            yield return line.Indent();
    }

    public string ToReason(bool satisfied)=>
        Statement.ToReason(satisfied);

    public string ToAssertion(TModel model, bool satisfied)
    {
        var parameter = expression.Parameters.First();
        return expression.ToExpressionAssertion(satisfied).Humanize(model, parameter);
    }
}
