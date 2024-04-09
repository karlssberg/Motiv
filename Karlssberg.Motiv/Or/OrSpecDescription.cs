using Karlssberg.Motiv.OrElse;

namespace Karlssberg.Motiv.Or;

internal sealed class OrSpecDescription<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : ISpecDescription
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
                orSpec.Description.Statement,
            OrElseSpec<TModel, TMetadata> orElseSpec =>
                orElseSpec.Description.Statement,
            IBinaryOperationSpec binarySpec =>
                $"({binarySpec.Description.Statement})",
            _ => operand.Description.Statement
        };
    }

    private string Explain(SpecBase<TModel, TMetadata> operand)
    {
        return operand switch
        {
            OrSpec<TModel, TMetadata> orSpec =>
                orSpec.Description.Detailed,
            OrElseSpec<TModel, TMetadata> orElseSpec =>
                orElseSpec.Description.Detailed,
            IBinaryOperationSpec binarySpec =>
                $"({binarySpec.Description.Detailed})",
            _ => operand.Description.Detailed
        };
    }

    public override string ToString() => Statement;
}