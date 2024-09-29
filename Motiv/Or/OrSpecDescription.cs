using Motiv.OrElse;
using Motiv.Traversal;

namespace Motiv.Or;

internal sealed class OrSpecDescription<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : ISpecDescription
{
    public string Statement => $"{Summarize(left)} | {Summarize(right)}";

    public string Detailed => string.Join(Environment.NewLine, GetDetailsAsLines());

    public IEnumerable<string> GetDetailsAsLines()
    {
        IEnumerable<SpecBase<TModel, TMetadata>> specs = [left, right];
        return specs.GetBinaryJustificationAsLines(Operator.Or);
    }

    private static string Summarize(SpecBase<TModel> operand)
    {
        return operand switch
        {
            OrSpec<TModel, TMetadata> orSpec =>
                orSpec.Statement,
            OrElseSpec<TModel, TMetadata> orElseSpec =>
                orElseSpec.Statement,
            OrElsePolicy<TModel, TMetadata> orElsePolicy =>
                orElsePolicy.Statement,
            IBinaryOperationSpec<TModel> binarySpec =>
                $"({binarySpec.Description.Statement})",
            _ => operand.Statement
        };
    }

    public override string ToString() => Statement;
}
