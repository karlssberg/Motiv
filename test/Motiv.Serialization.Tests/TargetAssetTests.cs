using Motiv.Testing;

namespace Motiv.Serialization.Tests;

/// <summary>
/// Pins which builds of Motiv.Serialization and Motiv each test leg exercises. Motiv is checked too
/// because it arrives as a transitive reference, which the SDK resolves on its own — pinning only
/// the direct reference would load a netstandard2.0 serializer over a net10.0 core.
/// </summary>
public class TargetAssetTests
{
    [Fact]
    public void Should_load_the_Motiv_Serialization_build_this_leg_is_pinned_to() =>
        TargetAsset.FrameworkOf(typeof(RuleSerializer).Assembly).ShouldBe(TargetAsset.Expected);

    [Fact]
    public void Should_load_the_Motiv_build_this_leg_is_pinned_to() =>
        TargetAsset.FrameworkOf(typeof(Spec).Assembly).ShouldBe(TargetAsset.Expected);

    [Theory]
    [InlineData(typeof(RuleSerializer))]
    [InlineData(typeof(Spec))]
    public void Should_resolve_every_assembly_the_loaded_build_references(Type type) =>
        TargetAsset.UnresolvedReferencesOf(type.Assembly).ShouldBeEmpty();
}
