using Motiv.Serialization;

namespace Motiv.Serialization.Tests;

public class RuleCompositionTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0).Create("is positive");

    private static SpecBase<int, string> IsEven { get; } =
        Spec.Build((int n) => n % 2 == 0).Create("is even");

    private static SpecBase<int, string> IsBig { get; } =
        Spec.Build((int n) => Math.Abs(n) > 100).Create("is big");

    private static SpecBase<int, string> Throws { get; } =
        Spec.Build((int _) => (bool)ThrowBoom()).Create("throws");

    private static object ThrowBoom() => throw new InvalidOperationException("boom");

    private static RuleSerializer CreateSerializer() =>
        new(new SpecRegistry()
            .Register("is-positive", IsPositive)
            .Register("is-even", IsEven)
            .Register("is-big", IsBig)
            .Register("throws", Throws));

    private static readonly int[] Models = [2, 3, -2, -3];

    private static void ShouldBehaveIdentically(
        SpecBase<int, string> loaded,
        SpecBase<int, string> expected)
    {
        foreach (var model in Models)
        {
            var expectedResult = expected.Evaluate(model);
            var actualResult = loaded.Evaluate(model);
            actualResult.Satisfied.ShouldBe(expectedResult.Satisfied);
            actualResult.Reason.ShouldBe(expectedResult.Reason);
            actualResult.Assertions.ShouldBe(expectedResult.Assertions);
            actualResult.Justification.ShouldBe(expectedResult.Justification);
        }
    }

    public static TheoryData<string, string> BinaryOperators => new()
    {
        { "and", "And" },
        { "or", "Or" },
        { "xor", "XOr" },
        { "andAlso", "AndAlso" },
        { "orElse", "OrElse" }
    };

    private static SpecBase<int, string> Compose(
        string method,
        SpecBase<int, string> left,
        SpecBase<int, string> right) =>
        method switch
        {
            "And" => left.And(right),
            "Or" => left.Or(right),
            "XOr" => left.XOr(right),
            "AndAlso" => left.AndAlso(right),
            _ => left.OrElse(right)
        };

    [Theory]
    [MemberData(nameof(BinaryOperators))]
    public void Should_bind_each_binary_operator_to_its_fluent_equivalent(string jsonOperator, string method)
    {
        // Arrange
        var json =
            $$"""{ "rule": { "{{jsonOperator}}": [ { "spec": "is-positive" }, { "spec": "is-even" } ] } }""";
        var expected = Compose(method, IsPositive, IsEven);

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected);
    }

    [Fact]
    public void Should_bind_not_to_the_fluent_negation()
    {
        // Arrange
        const string json = """{ "rule": { "not": { "spec": "is-positive" } } }""";
        var expected = IsPositive.Not();

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected);
    }

    [Fact]
    public void Should_fold_arrays_of_more_than_two_operands_to_the_left()
    {
        // Arrange
        const string json =
            """
            { "rule": { "and": [ { "spec": "is-positive" }, { "spec": "is-even" }, { "spec": "is-big" } ] } }
            """;
        var expected = IsPositive.And(IsEven).And(IsBig);

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected);
    }

    [Fact]
    public void Should_short_circuit_andAlso_like_the_fluent_operator()
    {
        // Arrange
        const string json =
            """{ "rule": { "andAlso": [ { "spec": "is-positive" }, { "spec": "throws" } ] } }""";
        var expected = IsPositive.AndAlso(Throws);

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert: -5 fails the left operand, so the throwing right operand is never evaluated
        var expectedResult = expected.Evaluate(-5);
        var actualResult = loaded.Evaluate(-5);
        actualResult.Satisfied.ShouldBe(expectedResult.Satisfied);
        actualResult.Reason.ShouldBe(expectedResult.Reason);
    }

    [Fact]
    public void Should_decorate_a_composition_node()
    {
        // Arrange
        const string json =
            """
            {
              "rule": {
                "or": [ { "spec": "is-positive" }, { "spec": "is-even" } ],
                "whenTrue": "acceptable",
                "whenFalse": "unacceptable",
                "name": "acceptability"
              }
            }
            """;
        var expected = Spec
            .Build(IsPositive.Or(IsEven))
            .WhenTrue("acceptable")
            .WhenFalse("unacceptable")
            .Create("acceptability");

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected);
    }

    [Fact]
    public void Should_load_a_nested_document_identically_to_its_fluent_equivalent()
    {
        // Arrange
        const string json =
            """
            {
              "name": "eligibility",
              "rule": {
                "andAlso": [
                  { "spec": "is-positive" },
                  { "not": { "spec": "is-big" } },
                  {
                    "or": [ { "spec": "is-even" }, { "spec": "is-big" } ],
                    "whenTrue": "shape is fine",
                    "whenFalse": "shape is wrong"
                  }
                ]
              }
            }
            """;
        var inner = Spec
            .Build(IsEven.Or(IsBig))
            .WhenTrue("shape is fine")
            .WhenFalse("shape is wrong")
            .Create();
        var expected = Spec
            .Build(IsPositive.AndAlso(IsBig.Not()).AndAlso(inner))
            .Create("eligibility");

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected);
    }

    [Fact]
    public void Should_collect_errors_from_every_operand()
    {
        // Arrange
        const string json =
            """{ "rule": { "and": [ { "spec": "missing-1" }, { "spec": "missing-2" } ] } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        exception.Errors.Count.ShouldBe(2);
        exception.Errors.ShouldAllBe(error => error.Code == RuleErrorCode.UnknownSpec);
    }
}
