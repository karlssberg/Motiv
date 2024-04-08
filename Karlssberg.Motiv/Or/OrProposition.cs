using Karlssberg.Motiv.OrElse;

namespace Karlssberg.Motiv.Or;

internal sealed class OrProposition<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : IProposition
{
    public string Statement => $"{Summarize(left)} | {Summarize(right)}";

    public string Detailed =>
        $"""
             {Explain(left).IndentAfterFirstLine()} |
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
            IBinaryOperationSpec binarySpec =>
                $"({binarySpec.Proposition.Statement})",
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
            IBinaryOperationSpec binarySpec =>
                $"({binarySpec.Proposition.Detailed})",
            _ => operand.Proposition.Detailed
        };
    }

    public override string ToString() => Statement;
}