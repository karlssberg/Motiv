using Motiv.Testing;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.Snapshots.Tests;

/// <summary>
/// Pins which builds of Motiv.Serialization.Snapshots — and the two it references transitively —
/// each test leg exercises. This project has no net472 leg, so before the netstandard2.0 asset leg
/// the netstandard2.0 build of Snapshots was shipped without ever being loaded by a test.
/// </summary>
public class TargetAssetTests
{
    [Theory]
    [InlineData(typeof(RuleSnapshot))]
    [InlineData(typeof(RuleSerializer))]
    [InlineData(typeof(Spec))]
    public void Should_load_the_build_this_leg_is_pinned_to(Type type) =>
        TargetAsset.FrameworkOf(type.Assembly).ShouldBe(TargetAsset.Expected);

    [Theory]
    [InlineData(typeof(RuleSnapshot))]
    public void Should_resolve_every_assembly_the_loaded_build_references(Type type) =>
        TargetAsset.UnresolvedReferencesOf(type.Assembly).ShouldBeEmpty();
}
