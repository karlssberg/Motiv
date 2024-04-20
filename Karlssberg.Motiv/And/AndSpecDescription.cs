using Karlssberg.Motiv.AndAlso;

namespace Karlssberg.Motiv.And;

internal sealed class AndSpecDescription<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : ISpecDescription
{
    public string Statement => $"{Summarize(left)} & {Summarize(right)}";

    public string Detailed =>
        $"""
             {Explain(left).IndentAfterFirstLine()} &
             {Explain(right).IndentAfterFirstLine()}
         """;

    private string Summarize(SpecBase<TModel, TMetadata> operand)
    {
        return operand switch
        {
            AndSpec<TModel, TMetadata> andSpec =>
                andSpec.Description.Statement,
            AndAlsoSpec<TModel, TMetadata> andAlsoSpec =>
                andAlsoSpec.Description.Statement,
            IBinaryOperationSpec binarySpec =>
                $"({binarySpec.Description.Statement})",
            _ => operand.Description.Statement
        };
    }

    private string Explain(SpecBase<TModel, TMetadata> operand)
    {
        return operand switch
        {
            AndSpec<TModel, TMetadata> andSpec =>
                andSpec.Description.Detailed,
            AndAlsoSpec<TModel, TMetadata> andAlsoSpec =>
                andAlsoSpec.Description.Detailed,
            IBinaryOperationSpec binarySpec =>
                $"({binarySpec.Description.Detailed})",
            _ => operand.Description.Detailed
        };
    }

    public override string ToString() => Statement;
}