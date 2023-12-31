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

public class InsightsVisitor<TMetadata>
{
    protected IEnumerable<TMetadata> Visit(IEnumerable<BooleanResultBase<TMetadata>> booleanResultBases) =>
        booleanResultBases.SelectMany(Visit);

    public IEnumerable<TMetadata> Visit(BooleanResultBase<TMetadata> booleanResultBase)
    {
        return booleanResultBase switch
        {
            AllBooleanResult<TMetadata> allBooleanResult => Visit(allBooleanResult),
            AndBooleanResult<TMetadata> andBooleanResult => Visit(andBooleanResult),
            AnyBooleanResult<TMetadata> anyBooleanResult => Visit(anyBooleanResult),
            AtLeastBooleanResult<TMetadata> atLeastBooleanResult => Visit(atLeastBooleanResult),
            AtMostBooleanResult<TMetadata> atMostBooleanResult => Visit(atMostBooleanResult),
            BooleanResult<TMetadata> booleanResult => Visit(booleanResult),
            IChangeMetadataTypeBooleanResult<TMetadata> changeMetadataTypeBooleanResult => Visit(changeMetadataTypeBooleanResult),
            NotBooleanResult<TMetadata> notBooleanResult => Visit(notBooleanResult),
            OrBooleanResult<TMetadata> orBooleanResult => Visit(orBooleanResult),
            SubstituteMetadataBooleanResult<TMetadata> substituteMetadataBooleanResult => Visit(substituteMetadataBooleanResult),
            XOrBooleanResult<TMetadata> xOrBooleanResult => Visit(xOrBooleanResult),
            _ => Enumerable.Empty<TMetadata>()
        };
    }

    protected virtual IEnumerable<TMetadata> Visit(AllBooleanResult<TMetadata> allBooleanResult) =>
        allBooleanResult.SubstituteMetadata.ElseIfEmpty(Visit(allBooleanResult.DeterminativeOperandResults));

    protected virtual IEnumerable<TMetadata> Visit(AndBooleanResult<TMetadata> andBooleanResult) =>
        Visit(andBooleanResult.DeterminativeOperandResults);

    protected virtual IEnumerable<TMetadata> Visit(AnyBooleanResult<TMetadata> anyBooleanResult) =>
        anyBooleanResult.SubstituteMetadata.ElseIfEmpty(Visit(anyBooleanResult.DeterminativeOperandResults));

    protected virtual IEnumerable<TMetadata> Visit(AtLeastBooleanResult<TMetadata> atLeastBooleanResult) =>
        atLeastBooleanResult.SubstituteMetadata
            .ElseIfEmpty(Visit(atLeastBooleanResult.DeterminativeOperandResults));

    protected virtual IEnumerable<TMetadata> Visit(AtMostBooleanResult<TMetadata> atMostBooleanResult) =>
        atMostBooleanResult.SubstituteMetadata
            .ElseIfEmpty(Visit(atMostBooleanResult.DeterminativeOperandResults));

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