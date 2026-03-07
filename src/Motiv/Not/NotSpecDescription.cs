using Motiv.Traversal;

namespace Motiv.Not;

internal sealed class NotSpecDescription<TModel, TMetadata>(SpecBase<TModel, TMetadata> operand) : ISpecDescription
{
    private string? _detailed;

    public string Statement => FormatStatement();

    public string Detailed => _detailed ??= string.Join(Environment.NewLine, GetDetailsAsLines());

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

    public string ToReason(bool satisfied)=>
        Statement.ToReason(satisfied);

    private string FormatStatement()
    {
        var firstChar = operand.Name.FirstOrDefault();
        return firstChar switch
        {
            '!' => operand.Name.Substring(1),
            not '!' when ContainsBinaryOperation(operand) =>  $"!({operand.Name})",
            _ =>  $"!{operand.Name}"
        };
    }

    private static bool ContainsBinaryOperation(SpecBase spec) =>
        spec switch
        {
            NotSpec<TModel, TMetadata> => false,
            NotPolicy<TModel, TMetadata> => false,
            IBinaryOperationSpec<TModel> => true,
            _ => spec.Underlying.Any(ContainsBinaryOperation)
        };

    public bool HasExplicitStatement => false;

    public override string ToString() => Statement;
}
