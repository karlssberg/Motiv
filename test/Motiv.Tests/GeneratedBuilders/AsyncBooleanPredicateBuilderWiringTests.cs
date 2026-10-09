namespace Motiv.Tests.GeneratedBuilders;

/// <summary>
/// Pins the Converj-generated builder methods reached from
/// <c>Spec.BuildAsync(predicate)</c>. Each test runs one generated method and
/// checks that the true and false outcomes each get their own payload, or that a quantifier step
/// evaluates as its name says.
/// </summary>
public class AsyncBooleanPredicateBuilderWiringTests
{
    [Fact]
    public async Task WhenTrueMetadataFunc1_WhenFalseMetadataValue()
    {
        var spec = Spec.BuildAsync((int n) => new ValueTask<bool>(n > 0))
            .WhenTrue(_ => Marker.True)
            .WhenFalse(Marker.False)
            .Create("subject");

        var whenTrue = await spec.EvaluateAsync(1);
        var whenFalse = await spec.EvaluateAsync(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe([Marker.True]);
        whenFalse.Values.ShouldBe([Marker.False]);
    }

    [Fact]
    public async Task WhenTrueString_WhenFalseFunc1()
    {
        var spec = Spec.BuildAsync((int n) => new ValueTask<bool>(n > 0))
            .WhenTrue("T")
            .WhenFalse(_ => "F")
            .Create("subject");

        var whenTrue = await spec.EvaluateAsync(1);
        var whenFalse = await spec.EvaluateAsync(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe(["T"]);
        whenFalse.Values.ShouldBe(["F"]);
    }

    [Fact]
    public async Task WhenTrueMetadataFunc1_WhenFalseMetadataFunc1()
    {
        var spec = Spec.BuildAsync((int n) => new ValueTask<bool>(n > 0))
            .WhenTrue(_ => Marker.True)
            .WhenFalse(_ => Marker.False)
            .Create("subject");

        var whenTrue = await spec.EvaluateAsync(1);
        var whenFalse = await spec.EvaluateAsync(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe([Marker.True]);
        whenFalse.Values.ShouldBe([Marker.False]);
    }

    [Fact]
    public async Task WhenTrueYieldFunc1_WhenFalseYieldFunc1()
    {
        var spec = Spec.BuildAsync((int n) => new ValueTask<bool>(n > 0))
            .WhenTrueYield(_ => ["T"])
            .WhenFalseYield(_ => ["F"])
            .Create("subject");

        var whenTrue = await spec.EvaluateAsync(1);
        var whenFalse = await spec.EvaluateAsync(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe(["T"]);
        whenFalse.Values.ShouldBe(["F"]);
    }

    [Fact]
    public async Task WhenTrueYieldFunc1_WhenFalseFunc1()
    {
        var spec = Spec.BuildAsync((int n) => new ValueTask<bool>(n > 0))
            .WhenTrueYield(_ => ["T"])
            .WhenFalse(_ => "F")
            .Create("subject");

        var whenTrue = await spec.EvaluateAsync(1);
        var whenFalse = await spec.EvaluateAsync(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe(["T"]);
        whenFalse.Values.ShouldBe(["F"]);
    }

    [Fact]
    public async Task WhenTrueYieldMetadataFunc1_WhenFalseMetadataFunc1()
    {
        var spec = Spec.BuildAsync((int n) => new ValueTask<bool>(n > 0))
            .WhenTrueYield(_ => new[] { Marker.True })
            .WhenFalse(_ => Marker.False)
            .Create("subject");

        var whenTrue = await spec.EvaluateAsync(1);
        var whenFalse = await spec.EvaluateAsync(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe([Marker.True]);
        whenFalse.Values.ShouldBe([Marker.False]);
    }

    [Fact]
    public async Task WhenTrueYieldMetadataFunc1_WhenFalseMetadataValue()
    {
        var spec = Spec.BuildAsync((int n) => new ValueTask<bool>(n > 0))
            .WhenTrueYield(_ => new[] { Marker.True })
            .WhenFalse(Marker.False)
            .Create("subject");

        var whenTrue = await spec.EvaluateAsync(1);
        var whenFalse = await spec.EvaluateAsync(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe([Marker.True]);
        whenFalse.Values.ShouldBe([Marker.False]);
    }
}
