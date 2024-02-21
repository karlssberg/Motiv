using Humanizer;

namespace Karlssberg.Motiv.XOr;

/// <summary>Represents the result of a logical XOR (exclusive OR) operation.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the result.</typeparam>
internal sealed class XOrBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> leftOperandResult,
    BooleanResultBase<TMetadata> rightOperandResult)
    : BooleanResultBase<TMetadata>
{
    /// <summary>Gets a value indicating whether the XOR operation is satisfied.</summary>
    public override bool Satisfied => leftOperandResult.Satisfied ^ rightOperandResult.Satisfied;

    internal override string DebuggerDisplay() => $"({leftOperandResult}) XOR:{IsSatisfiedDisplayText()} ({rightOperandResult})";

    public override Explanation Explanation => GetCausalResults().CreateExplanation();

    /// <summary>Gets the description of the XOR operation.</summary>
    public override string Description => GetCausalResults().Select(result => result.Description).Humanize();

    public override MetadataSet<TMetadata> Metadata => new(GetCausalResults()
        .SelectMany(result => result.Metadata));
    
    public override Cause<TMetadata> Cause => GetCausalResults().CreateCause();
    
    private IEnumerable<BooleanResultBase<TMetadata>> GetCausalResults() => [leftOperandResult, rightOperandResult];
}