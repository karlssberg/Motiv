using Motiv.Traversal;

namespace Motiv.Shared;

/// <summary>
///     A parameterized description for asynchronous binary spec operations (AND, OR, XOR, AND ALSO, OR ELSE).
/// </summary>
internal sealed class AsyncBinarySpecDescription<TModel, TMetadata>(
    AsyncSpecBase<TModel, TMetadata> left,
    AsyncSpecBase<TModel, TMetadata> right,
    string operatorSymbol,
    string operatorName,
    Func<SpecBase, bool> isSameFamily)
    : ISpecDescription
{
    public string Statement => field ??= $"{Summarize(left)} {operatorSymbol} {Summarize(right)}";

    public string Detailed => field ??= string.Join(Environment.NewLine, GetDetailsAsLines());

    public IEnumerable<string> GetDetailsAsLines()
    {
        IEnumerable<SpecBase> specs = [left, right];
        return specs.GetMixedBinaryJustificationAsLines<TModel, TMetadata>(operatorName);
    }

    public string ToReason(bool satisfied) =>
        Statement.ToReason(satisfied);

    public override string ToString() => Statement;

    private string Summarize(AsyncSpecBase<TModel> operand)
    {
        var subject = ((SpecBase)operand).Unwrap();
        return subject switch
        {
            _ when isSameFamily(subject) => operand.Name,
            IBinaryOperationSpec<TModel> => $"({operand.Description.Statement})",
            IAsyncBinaryOperationSpec => $"({operand.Description.Statement})",
            _ => operand.Name
        };
    }
}
