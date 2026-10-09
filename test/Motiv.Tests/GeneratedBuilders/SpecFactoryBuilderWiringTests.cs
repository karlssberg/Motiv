namespace Motiv.Tests.GeneratedBuilders;

public class SpecFactoryBuilderWiringTests
{
    private static readonly PolicyBase<int, Marker> IsPositiveWithMarkers =
        Spec.Build((int n) => n > 0)
            .WhenTrue(Marker.True)
            .WhenFalse(Marker.False)
            .Create("is positive");

    [Fact]
    public void Build_SpecFactory_WithMetadata_DecoratesTheSpecTheFactoryReturns()
    {
        Func<SpecBase<int, Marker>> factory = () => IsPositiveWithMarkers;

        var spec = Spec.Build(factory).Create("subject");

        spec.Evaluate(1).Satisfied.ShouldBeTrue();
        spec.Evaluate(-1).Satisfied.ShouldBeFalse();
        spec.Evaluate(1).Values.ShouldBe([Marker.True]);
        spec.Evaluate(-1).Values.ShouldBe([Marker.False]);
    }

    [Fact]
    public void Build_SpecFactory_WithStringMetadata_DecoratesTheSpecTheFactoryReturns()
    {
        Func<SpecBase<int, string>> factory = () => GeneratedBuilderFixtures.IsPositiveSpec;

        var spec = Spec.Build(factory).Create("subject");

        spec.Evaluate(1).Satisfied.ShouldBeTrue();
        spec.Evaluate(-1).Satisfied.ShouldBeFalse();
        spec.Evaluate(1).Values.ShouldBe(["is positive"]);
        spec.Evaluate(-1).Values.ShouldBe(["is not positive"]);
    }

    [Fact]
    public async Task Build_AsyncSpecFactory_WithMetadata_DecoratesTheSpecTheFactoryReturns()
    {
        Func<AsyncSpecBase<int, Marker>> factory = () => IsPositiveWithMarkers.ToAsyncSpec();

        var spec = Spec.Build(factory).Create("subject");

        (await spec.EvaluateAsync(1)).Satisfied.ShouldBeTrue();
        (await spec.EvaluateAsync(-1)).Satisfied.ShouldBeFalse();
        (await spec.EvaluateAsync(1)).Values.ShouldBe([Marker.True]);
        (await spec.EvaluateAsync(-1)).Values.ShouldBe([Marker.False]);
    }
}
