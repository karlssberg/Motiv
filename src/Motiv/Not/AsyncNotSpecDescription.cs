using Motiv.Traversal;

namespace Motiv.Not;

internal sealed class AsyncNotSpecDescription<TModel, TMetadata>(AsyncSpecBase<TModel, TMetadata> operand) : ISpecDescription
{
    public string Statement => field ??= FormatStatement();

    public string Detailed => field ??= string.Join(Environment.NewLine, GetDetailsAsLines());

    public IEnumerable<string> GetDetailsAsLines()
    {
        var lines = operand.Description
            .GetDetailsAsLines()
            .ReplaceFirstLine(firstLine =>
                ExpressionNegationMappings.Instance.TryGetValue(firstLine, out var mappedNegation)
                    ? mappedNegation
                    : firstLine.StartsWith("!")
                        ? firstLine.Substring(1)
                        : $"!{firstLine}");

        foreach (var line in lines)
            yield return line;
    }

    public string ToReason(bool satisfied) =>
        Statement.ToReason(satisfied);

    private string FormatStatement()
    {
        var firstChar = operand.Name.FirstOrDefault();
        return firstChar switch
        {
            '!' => operand.Name.Substring(1),
            not '!' when ContainsBinaryOperation(operand) => $"!({operand.Name})",
            _ => $"!{operand.Name}"
        };
    }

    private static bool ContainsBinaryOperation(SpecBase spec) =>
        spec switch
        {
            AsyncNotSpec<TModel, TMetadata> => false,
            AsyncNotPolicy<TModel, TMetadata> => false,
            NotSpec<TModel, TMetadata> => false,
            NotPolicy<TModel, TMetadata> => false,
            IAsyncBinaryOperationSpec => true,
            IBinaryOperationSpec<TModel> => true,
            _ => spec.Underlying.Any(ContainsBinaryOperation)
        };

    public override string ToString() => Statement;
}
