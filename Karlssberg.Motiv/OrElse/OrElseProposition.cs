using Karlssberg.Motiv.Or;

namespace Karlssberg.Motiv.OrElse;

internal sealed class OrElseProposition<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : IProposition
{
    public string Statement => $"{Summarize(left)} || {Summarize(right)}";

    public string Detailed =>
        $"""
         {Explain(left).IndentAfterFirstLine()} ||
         {Explain(right).IndentAfterFirstLine()}
         """;

    private string Summarize(SpecBase<TModel, TMetadata> operand)
    {
        return operand switch
        {
            OrSpec<TModel, TMetadata> orSpec =>
                orSpec.Proposition.Statement,
            OrElseSpec<TModel, TMetadata> orElseSpec =>
                orElseSpec.Proposition.Statement,
            ICompositeSpec compositeSpec =>
                $"({compositeSpec.Proposition.Statement})",
            _ => operand.Proposition.Statement
        };
    }

    private string Explain(SpecBase<TModel, TMetadata> operand)
    {
        return operand switch
        {
            OrSpec<TModel, TMetadata> orSpec =>
                orSpec.Proposition.Detailed,
            OrElseSpec<TModel, TMetadata> orElseSpec =>
                orElseSpec.Proposition.Detailed,
            ICompositeSpec compositeSpec =>
                $"({compositeSpec.Proposition.Detailed})",
            _ => operand.Proposition.Detailed
        };
    }

    public override string ToString() => Statement;
}