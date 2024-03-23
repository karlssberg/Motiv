namespace Karlssberg.Motiv.ElseIf;

internal sealed class ConsequentBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> antecedentResult,
    BooleanResultBase<TMetadata> consequentResult)
    : BooleanResultBase<TMetadata>
{
    public override bool Satisfied => consequentResult.Satisfied;
    public override ResultDescriptionBase Description => 
        new ConsequentBooleanResultDescription<TMetadata>(antecedentResult, consequentResult);
    public override ExplanationTree ExplanationTree => GetCauses().CreateExplanation();
    public override MetadataTree<TMetadata> MetadataTree => consequentResult.MetadataTree;
    public override IEnumerable<BooleanResultBase> Underlying => consequentResult.ToEnumerable();
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata => consequentResult.ToEnumerable();
    public override IEnumerable<BooleanResultBase> Causes => consequentResult.ToEnumerable();
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata => consequentResult.ToEnumerable();

    public IEnumerable<BooleanResultBase<TMetadata>> GetCauses()
    {
        yield return antecedentResult;
        yield return consequentResult;
    }
}