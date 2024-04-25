using Karlssberg.Motiv.AndAlso;

namespace Karlssberg.Motiv.And;

internal sealed class AndSpecDescription<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : ISpecDescription
{
    public string Statement => $"{Summarize(left)} & {Summarize(right)}";

    public string Detailed => string.Join(Environment.NewLine, GetDetailsAsLines());

    public IEnumerable<string> GetDetailsAsLines()
    {
        IEnumerable<SpecBase<TModel, TMetadata>> specs = [left, right];
        return specs.GetBinaryDetailsAsLines("AND");
    }

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

    public override string ToString() => Statement;
}