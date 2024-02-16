using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.Visitors;

/// <summary>Represents a default implementation of the insights visitor for a specific metadata type.</summary>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class DefaultMetadataVisitor<TMetadata>
{
    /// <summary>
    /// Visits a collection of <see cref="BooleanResultBase{TMetadata}" /> objects and returns a collection of
    /// <typeparamref name="TMetadata" />.
    /// </summary>
    /// <param name="booleanResultBases">The collection of <see cref="BooleanResultBase{TMetadata}" /> objects to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata" />.</returns>
    public IEnumerable<TMetadata> Visit(IEnumerable<BooleanResultBase<TMetadata>> booleanResultBases) =>
        booleanResultBases.SelectMany(Visit);

    /// <summary>
    /// Visits a <see cref="BooleanResultBase{TMetadata}" /> and returns a collection of
    /// <typeparamref name="TMetadata" />.
    /// </summary>
    /// <param name="booleanResultBase">The boolean result to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata" />.</returns>
    public IEnumerable<TMetadata> Visit(BooleanResultBase<TMetadata> booleanResultBase)
    {
        return booleanResultBase switch
        {
            ILogicalOperatorResult<TMetadata> logicalOperatorResult => Visit(logicalOperatorResult),
            IHigherOrderLogicalOperatorResult<TMetadata> logicalOperatorResult => Visit(logicalOperatorResult),
            IChangeMetadataBooleanResult<TMetadata> changeMetadataBooleanResult => Visit(changeMetadataBooleanResult),
            IChangeHigherOrderMetadataBooleanResult<TMetadata> changeMetadataBooleanResult => Visit(changeMetadataBooleanResult),
            IPropositionResult<TMetadata> propositionResult => Visit(propositionResult),
            _ => booleanResultBase.Causes.SelectMany(Visit)
        };
    }

    public virtual IEnumerable<TMetadata> Visit(IPropositionResult<TMetadata> propositionResult)
    {
        yield return propositionResult.Metadata;
    }

    public virtual IEnumerable<TMetadata> Visit(ILogicalOperatorResult<TMetadata> logicalOperatorResult) =>
        Visit(logicalOperatorResult.Causes);
    
    
    public virtual IEnumerable<TMetadata> Visit(IHigherOrderLogicalOperatorResult<TMetadata> logicalOperatorResult) =>
        Visit(logicalOperatorResult.Causes);

    public virtual IEnumerable<TMetadata> Visit(IChangeMetadataBooleanResult<TMetadata> changeMetadataBooleanResult)
    {
        return changeMetadataBooleanResult.Metadata;
    }
    public virtual IEnumerable<TMetadata> Visit(IChangeHigherOrderMetadataBooleanResult<TMetadata> changeMetadataBooleanResult)
    {
        return changeMetadataBooleanResult.Metadata;
    }
    
}