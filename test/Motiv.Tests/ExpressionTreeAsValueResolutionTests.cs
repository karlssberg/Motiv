namespace Motiv.Tests;

/// <summary>
/// <c>Display.AsValue(x)</c> prints the value of <c>x</c> rather than its source text. These tests pin how a
/// value that does not depend on the model is resolved — whether it is a captured local, a member of the
/// enclosing instance, a static member or a member chain — in both the construction-time text (the
/// justification's statement line) and the evaluation-time text (the assertions).
/// </summary>
public class ExpressionTreeAsValueResolutionTests
{
    private readonly int _instanceLimit = 5;

    private int InstanceLimit => 5;

    private const string ExpectedJustification =
        """
        at limit == true
            (int n) => n == 5 == true
                n == 5
        """;

    [Fact]
    public void Should_print_the_value_of_a_field_on_the_enclosing_instance()
    {
        var sut = Spec
            .From((int n) => n == Display.AsValue(_instanceLimit))
            .Create("at limit");

        var act = sut.Evaluate(5);

        act.Justification.ShouldBe(ExpectedJustification);
    }

    [Fact]
    public void Should_print_the_value_of_a_property_on_the_enclosing_instance()
    {
        var sut = Spec
            .From((int n) => n == Display.AsValue(InstanceLimit))
            .Create("at limit");

        var act = sut.Evaluate(5);

        act.Justification.ShouldBe(ExpectedJustification);
    }

    private class Limits(int upper)
    {
        public int Upper { get; } = upper;
    }

    private static int StaticLimit = 5;

    [Fact]
    public void Should_print_the_value_at_the_end_of_a_captured_member_chain()
    {
        var limits = new Limits(5);
        var sut = Spec
            .From((int n) => n == Display.AsValue(limits.Upper))
            .Create("at limit");

        var act = sut.Evaluate(5);

        act.Justification.ShouldBe(ExpectedJustification);
    }

    [Fact]
    public void Should_print_the_value_of_a_static_field()
    {
        var sut = Spec
            .From((int n) => n == Display.AsValue(StaticLimit))
            .Create("at limit");

        var act = sut.Evaluate(5);

        act.Justification.ShouldBe(ExpectedJustification);
    }

    [Fact]
    public void Should_construct_when_a_captured_member_chain_passes_through_null()
    {
        Limits? limits = null;

        var act = () => Spec
            .From((int n) => n == Display.AsValue(limits!.Upper))
            .Create("at limit");

        act.ShouldNotThrow();
    }

    [Fact]
    public void Should_print_the_name_of_a_variable_captured_from_an_enclosing_scope()
    {
        var outer = 5;
        var specs = new List<ExpressionSpecBase<int, string>>();
        for (var offset = 0; offset < 1; offset++)
        {
            var inner = offset;
            specs.Add(Spec.From((int n) => n == outer + inner).Create("at limit"));
        }

        var act = specs[0].Evaluate(5);

        act.Assertions.ShouldBe(["n == outer + inner"]);
    }

    [Theory]
    [InlineData(6, "n != 6")]
    [InlineData(7, "n != 7")]
    public void Should_read_a_captured_value_live_when_asserting(int limitAtEvaluation, string expectedAssertion)
    {
        var limits = new MutableLimits { Upper = 5 };
        var sut = Spec
            .From((int n) => n == Display.AsValue(limits.Upper))
            .Create("at limit");

        limits.Upper = limitAtEvaluation;
        var act = sut.Evaluate(5);

        act.Assertions.ShouldBe([expectedAssertion]);
    }

    private class MutableLimits
    {
        public int Upper { get; set; }
    }

    private static int ThrowingLimit => throw new InvalidOperationException("not configured");

    [Fact]
    public void Should_construct_when_a_captured_property_throws()
    {
        var act = () => Spec
            .From((int n) => n == Display.AsValue(ThrowingLimit))
            .Create("at limit");

        act.ShouldNotThrow();
    }

    private static SpecBase<int, string> ThrowingSpec => throw new InvalidOperationException("not configured");

    [Fact]
    public void Should_surface_the_getters_own_exception_when_a_captured_spec_property_throws()
    {
        var act = () => Spec
            .From((int[] ns) => ns.All(ThrowingSpec))
            .Create("all within limit");

        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe("not configured");
    }

    [Fact]
    public void Should_print_a_captured_enum_value_the_same_way_at_construction_and_evaluation()
    {
        var kind = DateTimeKind.Utc;
        var sut = Spec
            .From((DateTimeKind k) => k == Display.AsValue(kind))
            .Create("is utc");

        var act = sut.Evaluate(DateTimeKind.Utc);

        act.Justification.ShouldBe(
            """
            is utc == true
                (DateTimeKind k) => k == DateTimeKind.Utc == true
                    k == DateTimeKind.Utc
            """);
    }

    private static int GetLimit() => 5;

    [Fact]
    public void Should_print_the_value_returned_by_a_method_call_that_does_not_use_the_model()
    {
        var sut = Spec
            .From((int n) => n == Display.AsValue(GetLimit()))
            .Create("at limit");

        var act = sut.Evaluate(5);

        act.Justification.ShouldBe(ExpectedJustification);
    }

    [Fact]
    public void Should_print_the_value_of_arithmetic_that_does_not_use_the_model()
    {
        var limit = 4;
        var sut = Spec
            .From((int n) => n == Display.AsValue(limit + 1))
            .Create("at limit");

        var act = sut.Evaluate(5);

        act.Justification.ShouldBe(ExpectedJustification);
    }

    [Fact]
    public void Should_keep_the_source_text_in_the_statement_when_the_value_depends_on_the_model()
    {
        var sut = Spec
            .From((int n) => n == Display.AsValue(n * 1))
            .Create("at limit");

        var act = sut.Evaluate(5);

        act.Justification.ShouldBe(
            """
            at limit == true
                (int n) => n == n * 1 == true
                    n == 5
            """);
    }

    [Fact]
    public void Should_print_the_value_of_a_local_captured_from_an_enclosing_scope()
    {
        // A loop body is its own closure scope, so Roslyn reaches `outer` through a link to the enclosing
        // scope's closure: Constant(loop closure).CS$<>8__locals1.outer
        var outer = 5;
        var specs = new List<ExpressionSpecBase<int, string>>();
        for (var offset = 0; offset < 1; offset++)
        {
            var inner = offset;
            specs.Add(Spec.From((int n) => n == Display.AsValue(outer) + inner).Create("at limit"));
        }

        var act = specs[0].Evaluate(5);

        act.Justification.ShouldBe(
            """
            at limit == true
                (int n) => n == 5 + inner == true
                    n == 5 + inner
            """);
    }
}
