using AutoFixture;
using AutoFixture.Kernel;

namespace Motiv.Tests.Customizations;

public class BooleanResult<T>(
    bool satisfied,
    MetadataNode<T> metadataTier,
    Explanation explanation,
    ResultDescriptionBase description,
    IEnumerable<BooleanResultBase<T>> causes,
    IEnumerable<BooleanResultBase<T>> underlying) : BooleanResultBase<T>
{
    public override bool Satisfied => satisfied;
    public override ResultDescriptionBase Description => description;
    public override Explanation Explanation => explanation;
    public override IEnumerable<BooleanResultBase> Causes => causes;
    public override IEnumerable<BooleanResultBase> Underlying => underlying;
    public override MetadataNode<T> MetadataTier => metadataTier;
    public override IEnumerable<BooleanResultBase<T>> CausesWithValues => causes;
    public override IEnumerable<BooleanResultBase<T>> UnderlyingWithValues => underlying;
}
public class BooleanResultBaseCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new TypeRelay(typeof(BooleanResultBase<>), typeof(BooleanResult<>)));
        fixture.Customizations.Add(new TypeRelay(typeof(BooleanResultBase), typeof(BooleanResult<object>)));
    }
}
