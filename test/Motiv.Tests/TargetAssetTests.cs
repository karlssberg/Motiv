using Motiv.Testing;

namespace Motiv.Tests;

/// <summary>
/// Pins which build of Motiv each test leg exercises. A <c>ProjectReference</c> resolves to the
/// nearest compatible asset, so without the netstandard2.0 asset leg only <c>net472</c> ever loads
/// the <c>netstandard2.0</c> build — and <c>net472</c> runs on Windows CI alone.
/// </summary>
public class TargetAssetTests
{
    [Fact]
    public void Should_load_the_Motiv_build_this_leg_is_pinned_to() =>
        TargetAsset.FrameworkOf(typeof(Spec).Assembly).ShouldBe(TargetAsset.Expected);

    [Theory]
    [InlineData(typeof(Spec))]
    public void Should_resolve_every_assembly_the_loaded_build_references(Type type) =>
        TargetAsset.UnresolvedReferencesOf(type.Assembly).ShouldBeEmpty();
}
