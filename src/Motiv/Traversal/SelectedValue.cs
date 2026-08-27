namespace Motiv.Traversal;

/// <summary>Resolves a chain of <see cref="ISelectedValueResult{TMetadata}" /> selections iteratively.</summary>
internal static class SelectedValue
{
    internal static TMetadata Of<TMetadata>(ISelectedValueResult<TMetadata> root)
    {
        var selected = root.Selected;

        while (selected is ISelectedValueResult<TMetadata> selection)
            selected = selection.Selected;

        return selected.Value;
    }
}
