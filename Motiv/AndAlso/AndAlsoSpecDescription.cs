using Motiv.And;

namespace Motiv.AndAlso;

internal sealed class AndAlsoSpecDescription<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right)
    : ISpecDescription
{
    public string Statement => $"{Summarize(left)} && {Summarize(right)}";

    public string Detailed => string.Join(Environment.NewLine, GetDetailsAsLines());

    public IEnumerable<string> GetDetailsAsLines()
    {
        IEnumerable<SpecBase<TModel, TMetadata>> specs = [left, right];
        return specs.GetBinaryJustificationAsLines("AND ALSO");
    }

    private static string Summarize(SpecBase<TModel> operand)
    {
        return operand switch
        {
            AndSpec<TModel, TMetadata> andSpec => andSpec.Statement,
            AndAlsoSpec<TModel, TMetadata> andAlsoSpec => andAlsoSpec.Statement,
            IBinaryOperationSpec binarySpec => $"({binarySpec.Description.Statement})",
            _ => operand.Statement
        };
    }

    public override string ToString() => Statement;
}