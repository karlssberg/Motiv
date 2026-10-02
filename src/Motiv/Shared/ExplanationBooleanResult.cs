namespace Motiv.Shared;

internal sealed class ExplanationBooleanResult(BooleanResultBase booleanResult) : BooleanResultBase<string>
{
    /// <summary>The result being lifted to string metadata; its description and underlying results are this one's.</summary>
    internal BooleanResultBase Operand => booleanResult;

    public override bool Satisfied => booleanResult.Satisfied;

    public override ResultDescriptionBase Description => booleanResult.Description;

    public override Explanation Explanation => booleanResult.Explanation;

    public override IEnumerable<BooleanResultBase> Underlying => booleanResult.Underlying;

    public override IEnumerable<BooleanResultBase> Causes => booleanResult.Causes;

    public override MetadataNode<string> MetadataTier =>
        field ??= new MetadataNode<string>(booleanResult.Explanation.Assertions, []);

    public override IEnumerable<BooleanResultBase<string>> CausesWithValues =>
        booleanResult switch
        {
            BooleanResultBase<string> stringResult => stringResult.CausesWithValues,
            _ => []
        };

    public override IEnumerable<BooleanResultBase<string>> UnderlyingWithValues =>
        booleanResult switch
        {
            BooleanResultBase<string> stringResult => stringResult.UnderlyingWithValues,
            _ => []
        };
}
