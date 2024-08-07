using Motiv.Or;

namespace Motiv.OrElse;

internal sealed class OrElseSpecDescription<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : ISpecDescription
{
    public string Statement => $"{Summarize(left)} || {Summarize(right)}";

    public string Detailed => string.Join(Environment.NewLine, GetDetailsAsLines());

    public IEnumerable<string> GetDetailsAsLines()
    {
        IEnumerable<SpecBase<TModel, TMetadata>> specs = [left, right];
        return specs.GetBinaryJustificationAsLines(Operator.OrElse);
    }

    private static string Summarize(SpecBase<TModel> operand)
    {
        return operand switch
        {
            OrSpec<TModel, TMetadata> orSpec =>
                orSpec.Statement,
            OrElsePolicy<TModel, TMetadata> orElseSpec =>
                orElseSpec.Statement,
            OrElseSpec<TModel, TMetadata> orElseSpec =>
                orElseSpec.Statement,
            IBinaryOperationSpec<TModel> binarySpec =>
                $"({binarySpec.Description.Statement})",
            _ => operand.Statement
        };
    }

    public override string ToString() => Statement;
}
