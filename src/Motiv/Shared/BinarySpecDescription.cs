using Motiv.Traversal;

namespace Motiv.Shared;

/// <summary>
///     A parameterized description for binary spec operations (AND, OR, XOR, AND ALSO, OR ELSE).
/// </summary>
internal sealed class BinarySpecDescription<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right,
    string operatorSymbol,
    string operatorName,
    Func<SpecBase<TModel>, bool> isSameFamily)
    : ISpecDescription
{
    private string? _detailed;

    public string Statement => $"{Summarize(left)} {operatorSymbol} {Summarize(right)}";

    public string Detailed => _detailed ??= string.Join(Environment.NewLine, GetDetailsAsLines());

    public IEnumerable<string> GetDetailsAsLines()
    {
        IEnumerable<SpecBase<TModel, TMetadata>> specs = [left, right];
        return specs.GetBinaryJustificationAsLines(operatorName);
    }

    public string ToReason(bool satisfied) =>
        Statement.ToReason(satisfied);

    public bool HasExplicitStatement => false;

    public override string ToString() => Statement;

    private string Summarize(SpecBase<TModel> operand) =>
        operand switch
        {
            _ when isSameFamily(operand) => operand.Name,
            IBinaryOperationSpec<TModel> binarySpec => $"({binarySpec.Description.Statement})",
            _ => operand.Name
        };
}
