using Karlssberg.Motiv.AndAlso;

namespace Karlssberg.Motiv.And;

internal sealed class AndProposition<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : IProposition
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
                andSpec.Proposition.Statement,
            AndAlsoSpec<TModel, TMetadata> andAlsoSpec =>
                andAlsoSpec.Proposition.Statement,
            IBinaryOperationSpec binarySpec =>
                $"({binarySpec.Proposition.Statement})",
            _ => operand.Proposition.Statement
        };
    }

    private string Explain(SpecBase<TModel, TMetadata> operand)
    {
        return operand switch
        {
            AndSpec<TModel, TMetadata> andSpec =>
                andSpec.Proposition.Detailed,
            AndAlsoSpec<TModel, TMetadata> andAlsoSpec =>
                andAlsoSpec.Proposition.Detailed,
            IBinaryOperationSpec binarySpec =>
                $"({binarySpec.Proposition.Detailed})",
            _ => operand.Proposition.Detailed
        };
    }

    public override string ToString() => Statement;
}