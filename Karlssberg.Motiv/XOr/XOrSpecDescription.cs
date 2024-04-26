namespace Karlssberg.Motiv.XOr;

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
        return specs.GetBinaryDetailsAsLines("XOR");
    }

    private string Summarize(SpecBase<TModel, TMetadata> operand)
    {
        return operand switch 
        {
            XOrSpec<TModel, TMetadata> xOrSpec => xOrSpec.Description.Statement,
            IBinaryOperationSpec binarySpec => $"({binarySpec.Description.Statement})",
            _ => operand.Description.Statement
        };
    }
    
    public override string ToString() => Statement;
}