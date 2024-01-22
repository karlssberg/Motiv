﻿namespace Karlssberg.Motiv.XOr;

/// <summary>Represents the result of a logical XOR (exclusive OR) operation.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the result.</typeparam>
public sealed class XOrBooleanResult<TMetadata> : BooleanResultBase<TMetadata>, IBinaryBooleanResult<TMetadata>
{
    /// <summary>Initializes a new instance of the <see cref="XOrBooleanResult{TMetadata}" /> class.</summary>
    /// <param name="leftOperandResult">The result of the left operand.</param>
    /// <param name="rightOperandResult">The result of the right operand.</param>
    internal XOrBooleanResult(
        BooleanResultBase<TMetadata> leftOperandResult,
        BooleanResultBase<TMetadata> rightOperandResult)
    {
        IsSatisfied = leftOperandResult.IsSatisfied ^ rightOperandResult.IsSatisfied;
        LeftOperandResult = leftOperandResult ?? throw new ArgumentNullException(nameof(leftOperandResult));
        RightOperandResult = rightOperandResult ?? throw new ArgumentNullException(nameof(rightOperandResult));
    }

    /// <summary>Gets a value indicating whether the XOR operation is satisfied.</summary>
    public override bool IsSatisfied { get; }

    /// <summary>Gets the result of the left operand.</summary>
    public BooleanResultBase<TMetadata> LeftOperandResult { get; }

    /// <summary>Gets the result of the right operand.</summary>
    public BooleanResultBase<TMetadata> RightOperandResult { get; }

    /// <summary>Gets an array containing the left and right operand results.</summary>
    public IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults => [LeftOperandResult, RightOperandResult];

    /// <summary>Gets the determinative operand results.</summary>
    public IEnumerable<BooleanResultBase<TMetadata>> DeterminativeResults => UnderlyingResults;

    /// <summary>Gets the description of the XOR operation.</summary>
    public override string Description => $"({LeftOperandResult}) XOR:{IsSatisfiedDisplayText} ({RightOperandResult})";

    /// <summary>Gets the reasons for the XOR operation result.</summary>
    public override IEnumerable<string> GatherReasons() => DeterminativeResults.SelectMany(r => r.GatherReasons());
}