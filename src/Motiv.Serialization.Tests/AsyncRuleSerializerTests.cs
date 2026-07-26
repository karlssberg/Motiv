namespace Motiv.Serialization.Tests;

public class AsyncRuleSerializerTests
{
    // Plain classes (not records) so the net472 target compiles without an IsExternalInit polyfill.
    private sealed class Customer(bool isActive, int age)
    {
        public bool IsActive { get; } = isActive;
        public int Age { get; } = age;
    }

    private sealed class Verdict(string code)
    {
        public string Code { get; } = code;

        public override bool Equals(object? obj) => obj is Verdict other && other.Code == Code;

        public override int GetHashCode() => Code.GetHashCode();

        public override string ToString() => Code;
    }

    private static AsyncSpecBase<Customer, string> PassesCreditCheck { get; } =
        Spec.BuildAsync((Customer c) => new ValueTask<bool>(c.Age >= 21))
            .WhenTrue("passes credit check").WhenFalse("fails credit check").Create();

    private static AsyncSpecBase<Customer, Verdict> CreditVerdict { get; } =
        Spec.BuildAsync((Customer c) => new ValueTask<bool>(c.Age >= 21))
            .WhenTrue(new Verdict("CREDIT-OK")).WhenFalse(new Verdict("CREDIT-BAD")).Create("credit verdict");

    private static SpecRegistry Registry() => new SpecRegistry()
        .Register("credit-check", PassesCreditCheck)
        .Register("credit-verdict", CreditVerdict);

    // The parameter is interpolated into a payload string, per the substitution grammar.
    private const string ParameterizedJson =
        """
        {
          "parameters": { "label": { "type": "string" } },
          "rule": { "spec": "credit-check", "whenTrue": "{label}", "whenFalse": "no credit" }
        }
        """;

    // The declared parameter is required but unreferenced: object payloads are not interpolated,
    // so this exercises parameter resolution on the typed load without touching the payloads.
    private const string TypedParameterizedJson =
        """
        {
          "parameters": { "minAge": { "type": "integer" } },
          "rule": { "spec": "credit-verdict" }
        }
        """;

    [Fact]
    public void Should_accumulate_errors_in_validate_async_instead_of_throwing()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var errors = serializer.ValidateAsyncSpec<Customer>("""{ "rule": { "spec": "missing" } }""");

        // Assert
        errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.UnknownSpec);
    }

    [Fact]
    public void Should_accumulate_errors_in_typed_validate_async_instead_of_throwing()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var errors = serializer.ValidateAsyncSpec<Customer, Verdict>("""{ "rule": { "spec": "missing" } }""");

        // Assert
        errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.UnknownSpec);
    }

    [Fact]
    public void Should_validate_async_documents_that_reference_async_specs_as_valid()
    {
        // Arrange — the same document a sync Validate<TModel> rejects
        var serializer = new RuleSerializer(Registry());
        var document = """{ "rule": { "spec": "credit-check" } }""";

        // Act & Assert
        serializer.Validate<Customer>(document)
            .ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.AsyncSpecInSyncLoad);
        serializer.ValidateAsyncSpec<Customer>(document).ShouldBeEmpty();
    }

    [Fact]
    public void Should_validate_async_metadata_documents_that_reference_async_specs_as_valid()
    {
        // Arrange — the same document a sync Validate<TModel, TMetadata> rejects
        var serializer = new RuleSerializer(Registry());
        var document = """{ "rule": { "spec": "credit-verdict" } }""";

        // Act & Assert
        serializer.Validate<Customer, Verdict>(document)
            .ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.AsyncSpecInSyncLoad);
        serializer.ValidateAsyncSpec<Customer, Verdict>(document).ShouldBeEmpty();
    }

    [Fact]
    public void Should_short_circuit_string_metadata_validation_to_the_explanation_path()
    {
        // Arrange — string payloads are valid in an explanation load but not in a typed load
        var serializer = new RuleSerializer(Registry());
        var document = """{ "rule": { "spec": "credit-check", "whenTrue": "yes", "whenFalse": "no" } }""";

        // Act & Assert
        serializer.ValidateAsyncSpec<Customer, string>(document).ShouldBeEmpty();
        serializer.ValidateAsyncSpec<Customer, Verdict>(document)
            .ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
    }

    [Fact]
    public async Task Should_supply_parameters_to_async_loads_from_an_anonymous_object()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var spec = serializer.DeserializeAsyncSpec<Customer>(ParameterizedJson, new { label = "credit gate" });
        var result = await spec.EvaluateAsync(new Customer(true, 30));

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["credit gate"]);
    }

    [Fact]
    public async Task Should_supply_parameters_to_async_loads_from_a_dictionary()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());
        var parameters = new Dictionary<string, object?> { ["label"] = "credit gate" };

        // Act
        var spec = serializer.DeserializeAsyncSpec<Customer>(ParameterizedJson, parameters);
        var result = await spec.EvaluateAsync(new Customer(true, 30));

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["credit gate"]);
    }

    [Fact]
    public async Task Should_supply_parameters_to_typed_async_loads_from_an_anonymous_object()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var spec = serializer.DeserializeAsyncSpec<Customer, Verdict>(TypedParameterizedJson, new { minAge = 21 });
        var result = await spec.EvaluateAsync(new Customer(true, 30));

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Values.ShouldBe([new Verdict("CREDIT-OK")]);
    }

    [Fact]
    public async Task Should_supply_parameters_to_typed_async_loads_from_a_dictionary()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());
        var parameters = new Dictionary<string, object?> { ["minAge"] = 21 };

        // Act
        var spec = serializer.DeserializeAsyncSpec<Customer, Verdict>(TypedParameterizedJson, parameters);
        var result = await spec.EvaluateAsync(new Customer(true, 30));

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Values.ShouldBe([new Verdict("CREDIT-OK")]);
    }

    [Fact]
    public void Should_throw_when_a_required_parameter_is_not_supplied_to_an_async_load()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var act = () => serializer.DeserializeAsyncSpec<Customer>(ParameterizedJson);

        // Assert
        var error = act.ShouldThrow<RuleSerializationException>().Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MissingParameter);
        error.Path.ShouldBe("$.parameters.label");
    }

    [Fact]
    public void Should_throw_when_a_required_parameter_is_not_supplied_to_a_typed_async_load()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var act = () => serializer.DeserializeAsyncSpec<Customer, Verdict>(TypedParameterizedJson);

        // Assert
        var error = act.ShouldThrow<RuleSerializationException>().Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MissingParameter);
        error.Path.ShouldBe("$.parameters.minAge");
    }

    [Fact]
    public void Should_not_report_unsupplied_required_parameters_during_async_validation()
    {
        // Arrange — validation stands in type-shaped placeholders, so supply errors are only
        // reported by DeserializeAsyncSpec
        var serializer = new RuleSerializer(Registry());

        // Act & Assert
        serializer.ValidateAsyncSpec<Customer>(ParameterizedJson).ShouldBeEmpty();
        serializer.ValidateAsyncSpec<Customer, Verdict>(TypedParameterizedJson).ShouldBeEmpty();
    }

    [Fact]
    public void Should_not_crash_when_the_document_is_not_valid_json_at_all()
    {
        // Act
        var errors = new RuleSerializer(Registry()).ValidateAsyncSpec<Customer>("not json at all");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$");
    }

    [Fact]
    public void Should_not_crash_when_the_document_is_not_valid_json_at_all_for_a_metadata_validation()
    {
        // Act
        var errors = new RuleSerializer(Registry()).ValidateAsyncSpec<Customer, Verdict>("not json at all");

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.InvalidNode);
        error.Path.ShouldBe("$");
    }

    private sealed class Order(decimal total)
    {
        public decimal Total { get; } = total;
    }

    private sealed class Account(params Order[] orders)
    {
        public IReadOnlyList<Order> Orders { get; } = orders;
    }

    private static SpecRegistry HigherOrderRegistry() => new SpecRegistry()
        .Register("async-order-check",
            Spec.BuildAsync((Order _) => new ValueTask<bool>(true))
                .WhenTrue("checked").WhenFalse("unchecked").Create())
        .Register("async-order-verdict",
            Spec.BuildAsync((Order _) => new ValueTask<bool>(true))
                .WhenTrue(new Verdict("CHECKED")).WhenFalse(new Verdict("UNCHECKED")).Create("order checked"))
        .RegisterCollection<Account, Order>("orders", a => a.Orders);

    [Fact]
    public void Should_surface_async_specs_in_higher_order_subtrees_during_async_validation()
    {
        // Arrange — higher-order subtrees must stay synchronous even in an async load
        var serializer = new RuleSerializer(HigherOrderRegistry());
        const string document =
            """{ "rule": { "asAllSatisfied": { "spec": "async-order-check" }, "path": "orders", "name": "all checked" } }""";

        // Act
        var errors = serializer.ValidateAsyncSpec<Account>(document);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.AsyncSpecInHigherOrder);
        error.Message.ShouldContain("higher-order propositions evaluate synchronously");
    }

    [Fact]
    public void Should_surface_async_specs_in_higher_order_subtrees_during_typed_async_validation()
    {
        // Arrange — the same invariant through the typed serializer entry point
        var serializer = new RuleSerializer(HigherOrderRegistry());
        const string document =
            """{ "rule": { "asAllSatisfied": { "spec": "async-order-verdict" }, "path": "orders", "name": "all checked" } }""";

        // Act
        var errors = serializer.ValidateAsyncSpec<Account, Verdict>(document);

        // Assert
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.AsyncSpecInHigherOrder);
        error.Message.ShouldContain("higher-order propositions evaluate synchronously");
    }
}
