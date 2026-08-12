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

    [Fact]
    public void Should_not_invoke_the_factory_when_the_arguments_do_not_resolve()
    {
        // Arrange — a factory that records whether it ran at all
        var invoked = false;
        var registry = new SpecRegistry().RegisterParameterised(
            "list.count-at-least",
            [new RuleParameterDeclaration("n", RuleParameterType.Integer, false, null)],
            values =>
            {
                invoked = true;
                return Spec.Build((List<int> list) => list.Count >= (int)values["n"]!).Create("counted");
            });

        // Act
        var errors = new RuleSerializer(registry)
            .Validate<List<int>>("""{ "rule": { "spec": "list.count-at-least" } }""");

        // Assert
        errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.MissingParameter);
        invoked.ShouldBeFalse();
    }

    [Fact]
    public void Should_report_a_throwing_factory_as_an_error_rather_than_propagating_it()
    {
        // Arrange — the factory rejects an argument combination the declarations cannot express
        var serializer = new RuleSerializer(ARegistryWhoseFactoryThrows());

        // Act
        var errors = serializer.Validate<List<int>>(
            """{ "rule": { "spec": "list.count-at-least", "args": { "n": -1 } } }""");

        // Assert — Validate accumulates; it never lets host code escape as an exception
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.SpecFactoryFailed);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_surface_a_throwing_factory_as_a_serialization_exception_on_load()
    {
        // Arrange
        var serializer = new RuleSerializer(ARegistryWhoseFactoryThrows());

        // Act
        var exception = Should.Throw<RuleSerializationException>(() => serializer.Deserialize<List<int>>(
            """{ "rule": { "spec": "list.count-at-least", "args": { "n": -1 } } }"""));

        // Assert
        exception.Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.SpecFactoryFailed);
    }

    [Fact]
    public async Task Should_bind_and_evaluate_a_parameterised_spec_through_an_async_load()
    {
        // Arrange
        var serializer = new RuleSerializer(CreateRegistry());

        // Act
        var spec = serializer.DeserializeAsyncSpec<List<int>>(
            """{ "rule": { "spec": "list.count-at-least", "args": { "n": 2 } } }""");

        // Assert
        (await spec.EvaluateAsync([1, 2])).Satisfied.ShouldBeTrue();
        (await spec.EvaluateAsync([1, 2])).Assertions.ShouldBe(["has at least 2 items"]);
        (await spec.EvaluateAsync([1])).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_bind_and_evaluate_a_parameterised_spec_through_a_metadata_load()
    {
        // Arrange
        var serializer = new RuleSerializer(CreateRegistry());

        // Act
        var spec = serializer.Deserialize<List<int>, Outcome>(AMetadataDocument);

        // Assert
        spec.Evaluate([1, 2]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1, 2]).Values.ShouldBe([new Outcome { Code = "ENOUGH" }]);
        spec.Evaluate([1]).Values.ShouldBe([new Outcome { Code = "TOO_FEW" }]);
    }

    [Fact]
    public async Task Should_bind_and_evaluate_a_parameterised_spec_through_an_async_metadata_load()
    {
        // Arrange
        var serializer = new RuleSerializer(CreateRegistry());

        // Act
        var spec = serializer.DeserializeAsyncSpec<List<int>, Outcome>(AMetadataDocument);

        // Assert
        (await spec.EvaluateAsync([1, 2])).Satisfied.ShouldBeTrue();
        (await spec.EvaluateAsync([1, 2])).Values.ShouldBe([new Outcome { Code = "ENOUGH" }]);
        (await spec.EvaluateAsync([1])).Satisfied.ShouldBeFalse();
        (await spec.EvaluateAsync([1])).Values.ShouldBe([new Outcome { Code = "TOO_FEW" }]);
    }

    [Fact]
    public void Should_reject_args_on_a_plain_spec_in_every_load_shape()
    {
        // Arrange — all four binders share one resolution site, and this is what pins that
        var serializer = new RuleSerializer(CreateRegistry());
        const string json = """{ "rule": { "spec": "list.is-not-empty", "args": { "n": 1 } } }""";

        // Act
        IReadOnlyList<RuleError>[] errorsPerLoadShape =
        [
            serializer.Validate<List<int>>(json),
            serializer.ValidateAsyncSpec<List<int>>(json),
            serializer.Validate<List<int>, Outcome>(json),
            serializer.ValidateAsyncSpec<List<int>, Outcome>(json)
        ];

        // Assert
        foreach (var errors in errorsPerLoadShape)
            errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.UnexpectedArguments);
    }

    [Fact]
    public void Should_reject_a_declared_default_that_is_not_of_the_declared_type()
    {
        // Act — caught at declaration, not as a cast at the first evaluation
        var exception = Should.Throw<ArgumentException>(() =>
            new RuleParameterDeclaration("n", RuleParameterType.Integer, hasDefault: true, "two"));

        // Assert
        exception.Message.ShouldContain("'n'");
        exception.Message.ShouldContain("integer");
    }

    [Fact]
    public void Should_narrow_a_declared_default_to_the_declared_type()
    {
        // Arrange — a long default for an 'integer' parameter is what a caller most easily writes
        var declaration = new RuleParameterDeclaration("n", RuleParameterType.Integer, hasDefault: true, 1L);
        var registry = new SpecRegistry().RegisterParameterised(
            "list.count-at-least",
            [declaration],
            values => Spec.Build((List<int> list) => list.Count >= (int)values["n"]!).Create("counted"));

        // Act
        var spec = new RuleSerializer(registry)
            .Deserialize<List<int>>("""{ "rule": { "spec": "list.count-at-least" } }""");

        // Assert — the cast the factory makes is the one the declaration promised
        declaration.DefaultValue.ShouldBe(1);
        spec.Evaluate([1]).Satisfied.ShouldBeTrue();
        spec.Evaluate([]).Satisfied.ShouldBeFalse();
    }

    private const string AMetadataDocument =
        """
        {
          "rule": {
            "spec": "list.count-at-least",
            "args": { "n": 2 },
            "whenTrue": { "Code": "ENOUGH" },
            "whenFalse": { "Code": "TOO_FEW" },
            "name": "counted"
          }
        }
        """;

    private static SpecRegistry ARegistryWhoseFactoryThrows() =>
        new SpecRegistry().RegisterParameterised(
            "list.count-at-least",
            [new RuleParameterDeclaration("n", RuleParameterType.Integer, false, null)],
            values => (int)values["n"]! >= 0
                ? Spec.Build((List<int> list) => list.Count >= (int)values["n"]!).Create("counted")
                : throw new ArgumentOutOfRangeException(nameof(values), "'n' must not be negative"));

    private sealed class Outcome
    {
        public string Code { get; set; } = "";

        public override bool Equals(object? obj) => obj is Outcome other && other.Code == Code;

        public override int GetHashCode() => Code.GetHashCode();

        public override string ToString() => Code;
    }
}
