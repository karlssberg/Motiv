namespace Motiv;

internal sealed class ExplanationBooleanResult(BooleanResultBase booleanResult) : BooleanResultBase<string>
{
    public override bool Satisfied => booleanResult.Satisfied;

    public override ResultDescriptionBase Description => booleanResult.Description;

    public override Explanation Explanation => booleanResult.Explanation;

    public override IEnumerable<BooleanResultBase> Underlying => booleanResult.Underlying;

    public override IEnumerable<BooleanResultBase> Causes => booleanResult.Causes;
    public override MetadataNode<string> MetadataTier => 
        new(booleanResult.Explanation.Assertions, []);
    public override IEnumerable<BooleanResultBase<string>> CausesWithMetadata => [];
    public override IEnumerable<BooleanResultBase<string>> UnderlyingWithMetadata => [];
}
