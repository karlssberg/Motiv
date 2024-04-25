using Karlssberg.Motiv.Or;

namespace Karlssberg.Motiv.OrElse;

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
        return specs.GetBinaryDetailsAsLines("OR ELSE");
    }

    private string Summarize(SpecBase<TModel, TMetadata> operand)
    {
        return operand switch
        {
            OrSpec<TModel, TMetadata> orSpec =>
                orSpec.Description.Statement,
            OrElseSpec<TModel, TMetadata> orElseSpec =>
                orElseSpec.Description.Statement,
            IBinaryOperationSpec compositeSpec =>
                $"({compositeSpec.Description.Statement})",
            _ => operand.Description.Statement
        };
    }

    private IEnumerable<string> Explain(SpecBase<TModel, TMetadata> operand)
    {
        return operand switch
        {
            OrSpec<TModel, TMetadata> orSpec =>
                orSpec.Description.GetDetailsAsLines(),
            OrElseSpec<TModel, TMetadata> orElseSpec =>
                orElseSpec.Description.GetDetailsAsLines(),
            IBinaryOperationSpec binarySpec => binarySpec.Description.GetDetailsAsLines().IndentLines(),
            _ => operand.Description.GetDetailsAsLines()
        };
    }

    public override string ToString() => Statement;
}