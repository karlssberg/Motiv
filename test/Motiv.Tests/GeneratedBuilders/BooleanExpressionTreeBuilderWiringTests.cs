namespace Motiv.Tests.GeneratedBuilders;

/// <summary>
/// Pins the Converj-generated builder methods reached from
/// <c>Spec.From(boolExpression)</c>. Each test runs one generated method and
/// checks that the true and false outcomes each get their own payload, or that a quantifier step
/// evaluates as its name says.
/// </summary>
public class BooleanExpressionTreeBuilderWiringTests
{
    [Fact]
    public void WhenTrueFunc2_WhenFalseFunc2()
    {
        var spec = Spec.From((int n) => n > 0)
            .WhenTrue((_, _) => "T")
            .WhenFalse((_, _) => "F")
            .Create("subject");

        var whenTrue = spec.Evaluate(1);
        var whenFalse = spec.Evaluate(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe(["T"]);
        whenFalse.Values.ShouldBe(["F"]);
    }

    [Fact]
    public void AsWithSelector_WhenTrueYieldFunc1_WhenFalseFunc1()
    {
        var spec = Spec.From((int n) => n > 0)
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
    public void AsWithSelector_WhenTrueYieldMetadataFunc1_WhenFalseMetadataFunc1()
    {
        var spec = Spec.From((int n) => n > 0)
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
    public void AsAtMostNSatisfied()
    {
        var spec = Spec.From((int n) => n > 0)
            .AsAtMostNSatisfied(1)
            .Create("subject");

        spec.Evaluate([1, -1]).Satisfied.ShouldBeTrue();
        spec.Evaluate([-1, -2]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1, 2]).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void WhenTrueFunc2_WhenFalseString()
    {
        var spec = Spec.From((int n) => n > 0)
            .WhenTrue((_, _) => "T")
            .WhenFalse("F")
            .Create("subject");

        var whenTrue = spec.Evaluate(1);
        var whenFalse = spec.Evaluate(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe(["T"]);
        whenFalse.Values.ShouldBe(["F"]);
    }

    [Fact]
    public void WhenTrueMetadataFunc2_WhenFalseMetadataFunc2()
    {
        var spec = Spec.From((int n) => n > 0)
            .WhenTrue((_, _) => Marker.True)
            .WhenFalse((_, _) => Marker.False)
            .Create("subject");

        var whenTrue = spec.Evaluate(1);
        var whenFalse = spec.Evaluate(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe([Marker.True]);
        whenFalse.Values.ShouldBe([Marker.False]);
    }

    [Fact]
    public void WhenTrueMetadataFunc2_WhenFalseMetadataFunc1()
    {
        var spec = Spec.From((int n) => n > 0)
            .WhenTrue((_, _) => Marker.True)
            .WhenFalse(_ => Marker.False)
            .Create("subject");

        var whenTrue = spec.Evaluate(1);
        var whenFalse = spec.Evaluate(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe([Marker.True]);
        whenFalse.Values.ShouldBe([Marker.False]);
    }

    [Fact]
    public void WhenTrueMetadataFunc2_WhenFalseMetadataValue()
    {
        var spec = Spec.From((int n) => n > 0)
            .WhenTrue((_, _) => Marker.True)
            .WhenFalse(Marker.False)
            .Create("subject");

        var whenTrue = spec.Evaluate(1);
        var whenFalse = spec.Evaluate(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe([Marker.True]);
        whenFalse.Values.ShouldBe([Marker.False]);
    }

    [Fact]
    public void WhenTrueYieldFunc2_WhenFalseYieldFunc2()
    {
        var spec = Spec.From((int n) => n > 0)
            .WhenTrueYield((_, _) => ["T"])
            .WhenFalseYield((_, _) => ["F"])
            .Create("subject");

        var whenTrue = spec.Evaluate(1);
        var whenFalse = spec.Evaluate(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe(["T"]);
        whenFalse.Values.ShouldBe(["F"]);
    }

    [Fact]
    public void WhenTrueYieldFunc2_WhenFalseFunc2()
    {
        var spec = Spec.From((int n) => n > 0)
            .WhenTrueYield((_, _) => ["T"])
            .WhenFalse((_, _) => "F")
            .Create("subject");

        var whenTrue = spec.Evaluate(1);
        var whenFalse = spec.Evaluate(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe(["T"]);
        whenFalse.Values.ShouldBe(["F"]);
    }

    [Fact]
    public void WhenTrueYieldFunc2_WhenFalseFunc1()
    {
        var spec = Spec.From((int n) => n > 0)
            .WhenTrueYield((_, _) => ["T"])
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
    public void WhenTrueYieldFunc2_WhenFalseString()
    {
        var spec = Spec.From((int n) => n > 0)
            .WhenTrueYield((_, _) => ["T"])
            .WhenFalse("F")
            .Create("subject");

        var whenTrue = spec.Evaluate(1);
        var whenFalse = spec.Evaluate(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe(["T"]);
        whenFalse.Values.ShouldBe(["F"]);
    }

    [Fact]
    public void WhenTrueYieldMetadataFunc2_WhenFalseYieldMetadataFunc2()
    {
        var spec = Spec.From((int n) => n > 0)
            .WhenTrueYield((_, _) => new[] { Marker.True })
            .WhenFalseYield((_, _) => new[] { Marker.False })
            .Create("subject");

        var whenTrue = spec.Evaluate(1);
        var whenFalse = spec.Evaluate(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe([Marker.True]);
        whenFalse.Values.ShouldBe([Marker.False]);
    }

    [Fact]
    public void WhenTrueYieldMetadataFunc2_WhenFalseMetadataFunc2()
    {
        var spec = Spec.From((int n) => n > 0)
            .WhenTrueYield((_, _) => new[] { Marker.True })
            .WhenFalse((_, _) => Marker.False)
            .Create("subject");

        var whenTrue = spec.Evaluate(1);
        var whenFalse = spec.Evaluate(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe([Marker.True]);
        whenFalse.Values.ShouldBe([Marker.False]);
    }

    [Fact]
    public void WhenTrueYieldMetadataFunc2_WhenFalseMetadataFunc1()
    {
        var spec = Spec.From((int n) => n > 0)
            .WhenTrueYield((_, _) => new[] { Marker.True })
            .WhenFalse(_ => Marker.False)
            .Create("subject");

        var whenTrue = spec.Evaluate(1);
        var whenFalse = spec.Evaluate(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe([Marker.True]);
        whenFalse.Values.ShouldBe([Marker.False]);
    }

    [Fact]
    public void WhenTrueYieldMetadataFunc2_WhenFalseMetadataValue()
    {
        var spec = Spec.From((int n) => n > 0)
            .WhenTrueYield((_, _) => new[] { Marker.True })
            .WhenFalse(Marker.False)
            .Create("subject");

        var whenTrue = spec.Evaluate(1);
        var whenFalse = spec.Evaluate(-1);

        whenTrue.Satisfied.ShouldBeTrue();
        whenFalse.Satisfied.ShouldBeFalse();
        whenTrue.Values.ShouldBe([Marker.True]);
        whenFalse.Values.ShouldBe([Marker.False]);
    }

    [Fact]
    public void AsWithSelector_WhenTrueFunc1_WhenFalseFunc1()
    {
        var spec = Spec.From((int n) => n > 0)
            .As(results => results.All(r => r.Satisfied), (_, results) => results)
            .WhenTrue(_ => "T")
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
    public void AsWithSelector_WhenTrueFunc1_WhenFalseString()
    {
        var spec = Spec.From((int n) => n > 0)
            .As(results => results.All(r => r.Satisfied), (_, results) => results)
            .WhenTrue(_ => "T")
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
    public void AsWithSelector_WhenTrueYieldFunc1_WhenFalseYieldFunc1()
    {
        var spec = Spec.From((int n) => n > 0)
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
    public void AsWithSelector_WhenTrueYieldFunc1_WhenFalseString()
    {
        var spec = Spec.From((int n) => n > 0)
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
    public void AsWithSelector_WhenTrueMetadataFunc1_WhenFalseMetadataFunc1()
    {
        var spec = Spec.From((int n) => n > 0)
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
        var spec = Spec.From((int n) => n > 0)
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
    public void AsWithSelector_WhenTrueYieldMetadataFunc1_WhenFalseYieldMetadataFunc1()
    {
        var spec = Spec.From((int n) => n > 0)
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

    [Fact]
    public void AsWithSelector_WhenTrueYieldMetadataFunc1_WhenFalseMetadataValue()
    {
        var spec = Spec.From((int n) => n > 0)
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
    public void AsAtLeastNSatisfied()
    {
        var spec = Spec.From((int n) => n > 0)
            .AsAtLeastNSatisfied(2)
            .Create("subject");

        spec.Evaluate([1, 2]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1, 2, 3]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1, -1]).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void AsNoneSatisfied()
    {
        var spec = Spec.From((int n) => n > 0)
            .AsNoneSatisfied()
            .Create("subject");

        spec.Evaluate([-1, -2]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1, -1]).Satisfied.ShouldBeFalse();
        spec.Evaluate([1, 2]).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void AsNSatisfied()
    {
        var spec = Spec.From((int n) => n > 0)
            .AsNSatisfied(1)
            .Create("subject");

        spec.Evaluate([1, -1]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1, 2]).Satisfied.ShouldBeFalse();
        spec.Evaluate([-1, -2]).Satisfied.ShouldBeFalse();
    }
}
