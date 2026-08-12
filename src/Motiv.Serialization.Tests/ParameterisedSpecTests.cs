namespace Motiv.Serialization.Tests;

public class ParameterisedSpecTests
{
    private static SpecBase<List<int>, string> IsNotEmpty { get; } =
        Spec.Build((List<int> list) => list.Count > 0)
            .WhenTrue("list is not empty")
            .WhenFalse("list is empty")
            .Create();

    private static SpecRegistry CreateRegistry(bool withDefault = false) =>
        new SpecRegistry()
            .Register("list.is-not-empty", IsNotEmpty)
            .RegisterParameterised(
                "list.count-at-least",
                [new RuleParameterDeclaration("n", RuleParameterType.Integer, withDefault, withDefault ? 1 : null)],
                values => Spec.Build((List<int> list) => list.Count >= (int)values["n"]!)
                    .WhenTrue($"has at least {values["n"]} items")
                    .WhenFalse($"has fewer than {values["n"]} items")
                    .Create());

    [Fact]
    public void Should_bind_and_evaluate_a_parameterised_spec()
    {
        // Arrange
        var serializer = new RuleSerializer(CreateRegistry());

        // Act
        var spec = serializer.Deserialize<List<int>>(
            """{ "rule": { "spec": "list.count-at-least", "args": { "n": 2 } } }""");

        // Assert
        spec.Evaluate([1, 2]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1, 2]).Assertions.ShouldBe(["has at least 2 items"]);
        spec.Evaluate([1]).Satisfied.ShouldBeFalse();
        spec.Evaluate([1]).Assertions.ShouldBe(["has fewer than 2 items"]);
    }

    [Fact]
    public void Should_compose_a_parameterised_spec_with_other_nodes()
    {
        // Arrange
        var serializer = new RuleSerializer(CreateRegistry());

        // Act
        var spec = serializer.Deserialize<List<int>>(
            """
            {
              "rule": {
                "and": [
                  { "spec": "list.is-not-empty" },
                  { "spec": "list.count-at-least", "args": { "n": 3 } }
                ]
              }
            }
            """);

        // Assert
        spec.Evaluate([1, 2, 3]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1, 2]).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_reject_a_missing_argument()
    {
        // Arrange
        var serializer = new RuleSerializer(CreateRegistry());

        // Act
        var errors = serializer.Validate<List<int>>("""{ "rule": { "spec": "list.count-at-least" } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MissingParameter);
        error.Path.ShouldBe("$.rule.args.n");
    }

    [Fact]
    public void Should_reject_a_mistyped_argument()
    {
        // Arrange
        var serializer = new RuleSerializer(CreateRegistry());

        // Act
        var errors = serializer.Validate<List<int>>(
            """{ "rule": { "spec": "list.count-at-least", "args": { "n": "two" } } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ParameterTypeMismatch);
        error.Path.ShouldBe("$.rule.args.n");
    }

    [Fact]
    public void Should_reject_a_surplus_argument()
    {
        // Arrange
        var serializer = new RuleSerializer(CreateRegistry());

        // Act
        var errors = serializer.Validate<List<int>>(
            """{ "rule": { "spec": "list.count-at-least", "args": { "n": 2, "m": 3 } } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.SurplusParameter);
        error.Path.ShouldBe("$.rule.args.m");
    }

    [Fact]
    public void Should_reject_args_on_a_plain_spec()
    {
        // Arrange
        var serializer = new RuleSerializer(CreateRegistry());

        // Act
        var errors = serializer.Validate<List<int>>(
            """{ "rule": { "spec": "list.is-not-empty", "args": { "n": 1 } } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnexpectedArguments);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_reject_args_on_a_node_that_is_not_a_spec_reference()
    {
        // Arrange
        var serializer = new RuleSerializer(CreateRegistry());

        // Act
        var errors = serializer.Validate("""{ "rule": { "not": { "spec": "a" }, "args": { "n": 1 } } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$.rule.args");
    }

    [Fact]
    public void Should_reject_a_non_scalar_argument_value()
    {
        // Arrange
        var serializer = new RuleSerializer(CreateRegistry());

        // Act
        var errors = serializer.Validate(
            """{ "rule": { "spec": "list.count-at-least", "args": { "n": { "value": 2 } } } }""");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ParameterTypeMismatch);
        error.Path.ShouldBe("$.rule.args.n");
    }

    [Fact]
    public void Should_apply_a_declared_default_when_the_argument_is_omitted()
    {
        // Arrange
        var serializer = new RuleSerializer(CreateRegistry(withDefault: true));

        // Act
        var spec = serializer.Deserialize<List<int>>("""{ "rule": { "spec": "list.count-at-least" } }""");

        // Assert
        spec.Evaluate([1]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1]).Assertions.ShouldBe(["has at least 1 items"]);
        spec.Evaluate([]).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_reject_arguments_supplied_to_an_unknown_spec()
    {
        // Arrange
        var serializer = new RuleSerializer(CreateRegistry());

        // Act
        var errors = serializer.Validate<List<int>>(
            """{ "rule": { "spec": "list.absent", "args": { "n": 1 } } }""");

        // Assert
        errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.UnknownSpec);
    }
}
