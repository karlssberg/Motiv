using Motiv.Traversal;

namespace Motiv.XOr;

internal sealed class XOrSpecDescription<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : ISpecDescription
{

    public string Statement => $"{Summarize(left)} ^ {Summarize(right)}";
    public string Detailed => string.Join(Environment.NewLine, GetDetailsAsLines());

    public IEnumerable<string> GetDetailsAsLines()
    {
        var specs = left.ToEnumerable().Append(right); // reverse order for easier reading
        return specs.GetBinaryJustificationAsLines(Operator.XOr);
    }

    private static string Summarize(SpecBase<TModel> operand)
    {
        return operand switch
        {
            XOrSpec<TModel, TMetadata> xOrSpec => xOrSpec.Statement,
            IBinaryOperationSpec<TModel> binarySpec => $"({binarySpec.Description.Statement})",
            _ => operand.Statement
        };
    }

    public string ToAssertion(bool satisfied) => Statement.ToReason(satisfied);

    public override string ToString() => Statement;
}
