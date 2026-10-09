using static Motiv.Tests.GeneratedBuilders.GeneratedBuilderFixtures;

namespace Motiv.Tests.GeneratedBuilders;

/// <summary>
/// Pins the Converj-generated builder methods reached from
/// <c>Spec.From(model =&gt; spec.Evaluate(model))</c>. Each test runs one generated method and
/// checks that the true and false outcomes each get their own payload, or that a quantifier step
/// evaluates as its name says.
/// </summary>
public class ResultExpressionTreeBuilderWiringTests
{
    [Fact]
    public void WhenTrueFunc2_WhenFalseFunc2()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
    public void WhenTrueFunc2_WhenFalseFunc1()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .WhenTrue((_, _) => "T")
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
    public void WhenTrueFunc2_WhenFalseString()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
    public void WhenTrueFunc2_WhenFalseYieldFunc2()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .WhenTrue((_, _) => "T")
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
    public void WhenTrueString_WhenFalseFunc2()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .WhenTrue("T")
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
    public void WhenTrueString_WhenFalseFunc1()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .WhenTrue("T")
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
    public void WhenTrueString_WhenFalseString()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .WhenTrue("T")
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
    public void WhenTrueString_WhenFalseYieldFunc2()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .WhenTrue("T")
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
    public void WhenTrueMetadataFunc2_WhenFalseMetadataFunc2()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
    public void WhenTrueMetadataFunc2_WhenFalseYieldMetadataFunc2()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .WhenTrue((_, _) => Marker.True)
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
    public void WhenTrueYieldFunc2_WhenFalseYieldFunc2()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
    public void AsWithSelector_WhenTrueString_WhenFalseFunc1()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .As(results => results.All(r => r.Satisfied), (_, results) => results)
            .WhenTrue("T")
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
    public void AsWithSelector_WhenTrueString_WhenFalseString()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .As(results => results.All(r => r.Satisfied), (_, results) => results)
            .WhenTrue("T")
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
    public void AsWithSelector_WhenTrueString_WhenFalseYieldFunc1()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .As(results => results.All(r => r.Satisfied), (_, results) => results)
            .WhenTrue("T")
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
    public void AsWithSelector_WhenTrueYieldFunc1_WhenFalseYieldFunc1()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
    public void AsWithSelector_WhenTrueMetadataFunc1_WhenFalseYieldMetadataFunc1()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .As(results => results.All(r => r.Satisfied), (_, results) => results)
            .WhenTrue(_ => Marker.True)
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
    public void AsWithSelector_WhenTrueYieldMetadataFunc1_WhenFalseYieldMetadataFunc1()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
    public void AsWithSelector_WhenTrueYieldMetadataFunc1_WhenFalseMetadataFunc1()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
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
    public void WhenTrueFunc1_WhenFalseString()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .WhenTrue(_ => "T")
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
    public void WhenTrueMetadataFunc1_WhenFalseMetadataValue()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .WhenTrue(_ => Marker.True)
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
    public void WhenTrueMetadataValue_WhenFalseMetadataValue()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .WhenTrue(Marker.True)
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
    public void AsWithSelector()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .As(results => results.All(r => r.Satisfied), (_, results) => results)
            .Create("subject");

        spec.Evaluate([1, 2]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1, -1]).Satisfied.ShouldBeFalse();
        spec.Evaluate([-1, -2]).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void As()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .As(results => results.All(r => r.Satisfied))
            .Create("subject");

        spec.Evaluate([1, 2]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1, -1]).Satisfied.ShouldBeFalse();
        spec.Evaluate([-1, -2]).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void AsAtLeastNSatisfied()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .AsAtLeastNSatisfied(2)
            .Create("subject");

        spec.Evaluate([1, 2]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1, 2, 3]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1, -1]).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void AsAtMostNSatisfied()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .AsAtMostNSatisfied(1)
            .Create("subject");

        spec.Evaluate([1, -1]).Satisfied.ShouldBeTrue();
        spec.Evaluate([-1, -2]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1, 2]).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void AsNoneSatisfied()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .AsNoneSatisfied()
            .Create("subject");

        spec.Evaluate([-1, -2]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1, -1]).Satisfied.ShouldBeFalse();
        spec.Evaluate([1, 2]).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void AsNSatisfied()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .AsNSatisfied(1)
            .Create("subject");

        spec.Evaluate([1, -1]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1, 2]).Satisfied.ShouldBeFalse();
        spec.Evaluate([-1, -2]).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void AsWithSelector_WhenTrueMetadataValue_WhenFalseMetadataValue()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .As(results => results.All(r => r.Satisfied), (_, results) => results)
            .WhenTrue(Marker.True)
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
    public void AsAllSatisfied()
    {
        var spec = Spec.From((int n) => IsPositiveSpec.Evaluate(n))
            .AsAllSatisfied()
            .Create("subject");

        spec.Evaluate([1, 2]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1, -1]).Satisfied.ShouldBeFalse();
        spec.Evaluate([-1, -2]).Satisfied.ShouldBeFalse();
    }
}
