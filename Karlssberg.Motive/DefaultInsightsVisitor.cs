using Karlssberg.Motive.All;
using Karlssberg.Motive.And;
using Karlssberg.Motive.Any;
using Karlssberg.Motive.AtLeast;
using Karlssberg.Motive.AtMost;
using Karlssberg.Motive.ChangeMetadataType;
using Karlssberg.Motive.Not;
using Karlssberg.Motive.Or;
using Karlssberg.Motive.SubstituteMetadata;
using Karlssberg.Motive.XOr;

namespace Karlssberg.Motive;

public class DefaultInsightsVisitor<TMetadata>
{
    protected IEnumerable<TMetadata> Visit(IEnumerable<BooleanResultBase<TMetadata>> booleanResultBases) =>
        booleanResultBases.SelectMany(Visit);

    public IEnumerable<TMetadata> Visit(BooleanResultBase<TMetadata> booleanResultBase)
    {
        return booleanResultBase switch
        {
            AllSatisfiedBooleanResult<TMetadata> allBooleanResult => Visit(allBooleanResult),
            AndBooleanResult<TMetadata> andBooleanResult => Visit(andBooleanResult),
            AnySatisfiedBooleanResult<TMetadata> anyBooleanResult => Visit(anyBooleanResult),
            AtLeastNSatisfiedBooleanResult<TMetadata> atLeastBooleanResult => Visit(atLeastBooleanResult),
            AtMostNSatisfiedBooleanResult<TMetadata> atMostBooleanResult => Visit(atMostBooleanResult),
            BooleanResult<TMetadata> booleanResult => Visit(booleanResult),
            IChangeMetadataTypeBooleanResult<TMetadata> changeMetadataTypeBooleanResult => Visit(changeMetadataTypeBooleanResult),
            NotBooleanResult<TMetadata> notBooleanResult => Visit(notBooleanResult),
            OrBooleanResult<TMetadata> orBooleanResult => Visit(orBooleanResult),
            SubstituteMetadataBooleanResult<TMetadata> substituteMetadataBooleanResult => Visit(substituteMetadataBooleanResult),
            XOrBooleanResult<TMetadata> xOrBooleanResult => Visit(xOrBooleanResult),
            _ => Enumerable.Empty<TMetadata>()
        };
    }

    protected virtual IEnumerable<TMetadata> Visit(AllSatisfiedBooleanResult<TMetadata> allSatisfiedBooleanResult) =>
        allSatisfiedBooleanResult.SubstituteMetadata.ElseIfEmpty(Visit(allSatisfiedBooleanResult.DeterminativeOperandResults));

    protected virtual IEnumerable<TMetadata> Visit(AndBooleanResult<TMetadata> andBooleanResult) =>
        Visit(andBooleanResult.DeterminativeOperandResults);

    protected virtual IEnumerable<TMetadata> Visit(AnySatisfiedBooleanResult<TMetadata> anySatisfiedBooleanResult) =>
        anySatisfiedBooleanResult.SubstituteMetadata.ElseIfEmpty(Visit(anySatisfiedBooleanResult.DeterminativeOperandResults));

    protected virtual IEnumerable<TMetadata> Visit(AtLeastNSatisfiedBooleanResult<TMetadata> atLeastNSatisfiedBooleanResult) =>
        atLeastNSatisfiedBooleanResult.SubstituteMetadata
            .ElseIfEmpty(Visit(atLeastNSatisfiedBooleanResult.DeterminativeOperandResults));

    protected virtual IEnumerable<TMetadata> Visit(AtMostNSatisfiedBooleanResult<TMetadata> atMostNSatisfiedBooleanResult) =>
        atMostNSatisfiedBooleanResult.SubstituteMetadata
            .ElseIfEmpty(Visit(atMostNSatisfiedBooleanResult.DeterminativeOperandResults));

    protected virtual IEnumerable<TMetadata> Visit(BooleanResult<TMetadata> booleanResult) =>
        [booleanResult.Metadata];

    protected virtual IEnumerable<TMetadata> Visit(IChangeMetadataTypeBooleanResult<TMetadata> changeMetadataTypeBooleanResult) =>
        [changeMetadataTypeBooleanResult.Metadata];

    protected virtual IEnumerable<TMetadata> Visit(NotBooleanResult<TMetadata> notBooleanResult) =>
        Visit(notBooleanResult.OperandResult);

    protected virtual IEnumerable<TMetadata> Visit(OrBooleanResult<TMetadata> orBooleanResult) =>
        Visit(orBooleanResult.DeterminativeOperandResults);

    protected virtual IEnumerable<TMetadata> Visit(SubstituteMetadataBooleanResult<TMetadata> substituteMetadataBooleanResult) =>
        [substituteMetadataBooleanResult.SubstituteMetadata];

    protected virtual IEnumerable<TMetadata> Visit(XOrBooleanResult<TMetadata> xOrBooleanResult) =>
        Visit(xOrBooleanResult.DeterminativeOperandResults);
}