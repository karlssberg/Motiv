namespace Motiv.Tests.GeneratedBuilders;

/// <summary>
/// Pins the Converj-generated builder methods reached from
/// <c>Spec.Build(predicate)</c>. Each test runs one generated method and
/// checks that the true and false outcomes each get their own payload, or that a quantifier step
/// evaluates as its name says.
/// </summary>
public class BooleanPredicateBuilderWiringTests
{
    [Fact]
    public void AsWithSelector_WhenTrueYieldMetadataFunc1_WhenFalseMetadataFunc1()
    {
        var spec = Spec.Build((int n) => n > 0)
            .As(results => results.All(r => r.Satisfied), (_, results) => results)
            .WhenTrueYield(_ => new[] { Marker.True })
            .WhenFalse(_ => Marker.False)
            .Create("subject");

        var whenTrue = spec.Evaluate([1, 2]);
        var whenFalse = spec.Evaluate([1, -1]);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe([Marker.True]);
        whenFalse.Values.ShouldBe([Marker.False]);
    }

    [Fact]
    public void AsWithSelector_WhenTrueYieldMetadataFunc1_WhenFalseMetadataValue()
    {
        var spec = Spec.Build((int n) => n > 0)
            .As(results => results.All(r => r.Satisfied), (_, results) => results)
            .WhenTrueYield(_ => new[] { Marker.True })
            .WhenFalse(Marker.False)
            .Create("subject");

        var whenTrue = spec.Evaluate([1, 2]);
        var whenFalse = spec.Evaluate([1, -1]);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe([Marker.True]);
        whenFalse.Values.ShouldBe([Marker.False]);
    }

    [Fact]
    public void WhenTrueYieldFunc1_WhenFalseYieldFunc1()
    {
        var spec = Spec.Build((int n) => n > 0)
            .WhenTrueYield(_ => ["T"])
            .WhenFalseYield(_ => ["F"])
            .Create("subject");

        var whenTrue = spec.Evaluate(1);
        var whenFalse = spec.Evaluate(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe(["T"]);
        whenFalse.Values.ShouldBe(["F"]);
    }

    [Fact]
    public void WhenTrueYieldFunc1_WhenFalseFunc1()
    {
        var spec = Spec.Build((int n) => n > 0)
            .WhenTrueYield(_ => ["T"])
            .WhenFalse(_ => "F")
            .Create("subject");

        var whenTrue = spec.Evaluate(1);
        var whenFalse = spec.Evaluate(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe(["T"]);
        whenFalse.Values.ShouldBe(["F"]);
    }

    [Fact]
    public void AsWithSelector_WhenTrueMetadataFunc1_WhenFalseMetadataFunc1()
    {
        var spec = Spec.Build((int n) => n > 0)
            .As(results => results.All(r => r.Satisfied), (_, results) => results)
            .WhenTrue(_ => Marker.True)
            .WhenFalse(_ => Marker.False)
            .Create("subject");

        var whenTrue = spec.Evaluate([1, 2]);
        var whenFalse = spec.Evaluate([1, -1]);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe([Marker.True]);
        whenFalse.Values.ShouldBe([Marker.False]);
    }

    [Fact]
    public void AsWithSelector_WhenTrueMetadataFunc1_WhenFalseMetadataValue()
    {
        var spec = Spec.Build((int n) => n > 0)
            .As(results => results.All(r => r.Satisfied), (_, results) => results)
            .WhenTrue(_ => Marker.True)
            .WhenFalse(Marker.False)
            .Create("subject");

        var whenTrue = spec.Evaluate([1, 2]);
        var whenFalse = spec.Evaluate([1, -1]);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe([Marker.True]);
        whenFalse.Values.ShouldBe([Marker.False]);
    }

    [Fact]
    public void AsWithSelector_WhenTrueYieldFunc1_WhenFalseYieldFunc1()
    {
        var spec = Spec.Build((int n) => n > 0)
            .As(results => results.All(r => r.Satisfied), (_, results) => results)
            .WhenTrueYield(_ => ["T"])
            .WhenFalseYield(_ => ["F"])
            .Create("subject");

        var whenTrue = spec.Evaluate([1, 2]);
        var whenFalse = spec.Evaluate([1, -1]);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe(["T"]);
        whenFalse.Values.ShouldBe(["F"]);
    }

    [Fact]
    public void AsWithSelector_WhenTrueYieldFunc1_WhenFalseFunc1()
    {
        var spec = Spec.Build((int n) => n > 0)
            .As(results => results.All(r => r.Satisfied), (_, results) => results)
            .WhenTrueYield(_ => ["T"])
            .WhenFalse(_ => "F")
            .Create("subject");

        var whenTrue = spec.Evaluate([1, 2]);
        var whenFalse = spec.Evaluate([1, -1]);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe(["T"]);
        whenFalse.Values.ShouldBe(["F"]);
    }

    [Fact]
    public void AsWithSelector_WhenTrueYieldFunc1_WhenFalseString()
    {
        var spec = Spec.Build((int n) => n > 0)
            .As(results => results.All(r => r.Satisfied), (_, results) => results)
            .WhenTrueYield(_ => ["T"])
            .WhenFalse("F")
            .Create("subject");

        var whenTrue = spec.Evaluate([1, 2]);
        var whenFalse = spec.Evaluate([1, -1]);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe(["T"]);
        whenFalse.Values.ShouldBe(["F"]);
    }

    [Fact]
    public void AsWithSelector_WhenTrueYieldMetadataFunc1_WhenFalseYieldMetadataFunc1()
    {
        var spec = Spec.Build((int n) => n > 0)
            .As(results => results.All(r => r.Satisfied), (_, results) => results)
            .WhenTrueYield(_ => new[] { Marker.True })
            .WhenFalseYield(_ => new[] { Marker.False })
            .Create("subject");

        var whenTrue = spec.Evaluate([1, 2]);
        var whenFalse = spec.Evaluate([1, -1]);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe([Marker.True]);
        whenFalse.Values.ShouldBe([Marker.False]);
    }
}
