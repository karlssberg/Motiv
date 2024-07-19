using AutoFixture;
using AutoFixture.Kernel;

namespace Motiv.Tests.Customizations;

public class PolicyResult<TMetadata>(
    bool satisfied,
    MetadataNode<TMetadata> metadataTier,
    Explanation explanation,
    ResultDescriptionBase description,
    IEnumerable<BooleanResultBase<TMetadata>> causes,
    IEnumerable<BooleanResultBase<TMetadata>> underlying,
    IEnumerable<BooleanResultBase<TMetadata>> causesWithValues,
    IEnumerable<BooleanResultBase<TMetadata>> underlyingWithValues,
    TMetadata value) : PolicyResultBase<TMetadata>
{
    public override bool Satisfied { get; } = satisfied;
    public override ResultDescriptionBase Description { get; } = description;
    public override Explanation Explanation { get; } = explanation;
    public override IEnumerable<BooleanResultBase> Causes { get; } = causes;
    public override IEnumerable<BooleanResultBase> Underlying { get; } = underlying;
    public override MetadataNode<TMetadata> MetadataTier { get; } = metadataTier;
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues { get; } = causesWithValues;
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues { get; } = underlyingWithValues;
    public override TMetadata Value { get; } = value;
}

public class PolicyResultBaseCustomization : ICustomization
{

    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new TypeRelay(typeof(PolicyResultBase<>), typeof(PolicyResult<>)));
    }
}
