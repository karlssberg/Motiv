using static Motiv.Serialization.Tests.SpecAssertions;

namespace Motiv.Serialization.Tests;

public class AsyncMetadataRuleBinderTests
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

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive)
            .WhenTrue("customer is active").WhenFalse("customer is inactive").Create();

    private static SpecBase<Customer, Verdict> ActiveVerdict { get; } =
        Spec.Build((Customer c) => c.IsActive)
            .WhenTrue(new Verdict("ACTIVE")).WhenFalse(new Verdict("INACTIVE")).Create("active verdict");

    private static AsyncSpecBase<Customer, string> PassesCreditCheck { get; } =
        Spec.BuildAsync((Customer c) => new ValueTask<bool>(c.Age >= 21))
            .WhenTrue("passes credit check").WhenFalse("fails credit check").Create();

    private static AsyncSpecBase<Customer, Verdict> CreditVerdict { get; } =
        Spec.BuildAsync((Customer c) => new ValueTask<bool>(c.Age >= 21))
            .WhenTrue(new Verdict("CREDIT-OK")).WhenFalse(new Verdict("CREDIT-BAD")).Create("credit verdict");

    private static SpecRegistry Registry() => new SpecRegistry()
        .Register("is-active", IsActive)
        .Register("active-verdict", ActiveVerdict)
        .Register("credit-check", PassesCreditCheck)
        .Register("credit-verdict", CreditVerdict)
        .Register("other-model-async",
            Spec.BuildAsync((string _) => new ValueTask<bool>(true))
                .WhenTrue(new Verdict("T")).WhenFalse(new Verdict("F")).Create("other model"));

    // Covers active/inactive crossed with creditworthy (21+) and not.
    private static readonly Customer[] Models =
    [
        new(true, 30), new(false, 30), new(true, 18), new(false, 18)
    ];

    [Fact]
    public async Task Should_bind_object_payloads_over_an_async_subtree()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());
        var document = """
            {
              "rule": {
                "and": [ { "spec": "is-active" }, { "spec": "credit-check" } ],
                "name": "approved",
                "whenTrue": { "Code": "OK" },
                "whenFalse": { "Code": "DENIED" }
              }
            }
            """;

        // Act
        var spec = serializer.DeserializeAsyncSpec<Customer, Verdict>(document);
        var result = await spec.EvaluateAsync(new Customer(true, 30));

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Values.ShouldBe([new Verdict("OK")]);
        result.Assertions.ShouldBe(["approved == true"]);
        var unsatisfied = await spec.EvaluateAsync(new Customer(false, 30));
        unsatisfied.Satisfied.ShouldBeFalse();
        unsatisfied.Values.ShouldBe([new Verdict("DENIED")]);
        unsatisfied.Assertions.ShouldBe(["approved == false"]);
    }

    [Fact]
    public async Task Should_route_string_metadata_loads_to_the_explanation_path()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var spec = serializer.DeserializeAsyncSpec<Customer, string>("""{ "rule": { "spec": "credit-check" } }""");
        var result = await spec.EvaluateAsync(new Customer(true, 30));

        // Assert
        result.Assertions.ShouldBe(["passes credit check"]);
    }

    [Fact]
    public void Should_report_string_payloads_in_metadata_loads()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());
        var document = """
            { "rule": { "spec": "credit-check", "whenTrue": "yes", "whenFalse": "no" } }
            """;

        // Act
        var act = () => serializer.DeserializeAsyncSpec<Customer, Verdict>(document);

        // Assert
        act.ShouldThrow<RuleSerializationException>()
            .Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
    }

    [Fact]
    public async Task Should_load_a_fully_sync_document_identically_to_the_sync_metadata_load()
    {
        // Arrange — a fully-sync document loaded async must match the sync metadata load exactly,
        // including the values surfaced by the evaluation.
        var serializer = new RuleSerializer(Registry());
        const string document =
            """
            {
              "rule": {
                "and": [
                  { "spec": "active-verdict" },
                  { "spec": "is-active",
                    "whenTrue": { "Code": "OK" },
                    "whenFalse": { "Code": "BAD" },
                    "name": "coded" }
                ]
              }
            }
            """;

        // Act
        var syncLoaded = serializer.Deserialize<Customer, Verdict>(document);
        var asyncLoaded = serializer.DeserializeAsyncSpec<Customer, Verdict>(document);

        // Assert
        await ShouldBehaveIdenticallyAsync(asyncLoaded, syncLoaded, Models);
    }

    [Fact]
    public async Task Should_compose_typed_specs_across_the_async_boundary_with_aggregated_values()
    {
        // Arrange — sync left is lifted, async right is used directly
        const string json =
            """{ "rule": { "and": [ { "spec": "active-verdict" }, { "spec": "credit-verdict" } ] } }""";
        var expected = ActiveVerdict.ToAsyncSpec().And(CreditVerdict);

        // Act
        var loaded = new RuleSerializer(Registry()).DeserializeAsyncSpec<Customer, Verdict>(json);

        // Assert — both operands causal aggregates both values; a lone causal operand surfaces
        // only its own value, exactly as the sync composition de-noises
        var bothCausal = await loaded.EvaluateAsync(new Customer(true, 30));
        bothCausal.Values.ShouldBe([new Verdict("ACTIVE"), new Verdict("CREDIT-OK")]);
        var singleCausal = await loaded.EvaluateAsync(new Customer(true, 18));
        singleCausal.Values.ShouldBe([new Verdict("CREDIT-BAD")]);
        await ShouldBehaveIdenticallyAsync(loaded, expected, Models);
    }

    [Fact]
    public void Should_report_a_payload_that_cannot_deserialize_to_the_metadata_type()
    {
        // Arrange — Code is a string; an object payload for it fails STJ deserialization
        var serializer = new RuleSerializer(Registry());
        const string document =
            """
            {
              "rule": {
                "spec": "credit-check",
                "whenTrue": { "Code": { "nested": true } },
                "whenFalse": { "Code": "BAD" },
                "name": "coded"
              }
            }
            """;

        // Act
        var act = () => serializer.DeserializeAsyncSpec<Customer, Verdict>(document);

        // Assert
        var error = act.ShouldThrow<RuleSerializationException>().Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
        error.Path.ShouldBe("$.rule.whenTrue");
    }

    [Fact]
    public void Should_accumulate_a_payload_failure_alongside_a_sibling_error()
    {
        // Arrange — a bad payload on one operand and an unknown spec on its sibling must both surface
        var serializer = new RuleSerializer(Registry());
        const string document =
            """
            {
              "rule": {
                "and": [
                  { "spec": "credit-check",
                    "whenTrue": { "Code": { "nested": true } },
                    "whenFalse": { "Code": "BAD" },
                    "name": "coded" },
                  { "spec": "missing" }
                ]
              }
            }
            """;

        // Act
        var act = () => serializer.DeserializeAsyncSpec<Customer, Verdict>(document);

        // Assert
        var errors = act.ShouldThrow<RuleSerializationException>().Errors;
        errors.Count.ShouldBe(2);
        errors.ShouldContain(error =>
            error.Code == RuleErrorCode.MetadataTypeMismatch && error.Path == "$.rule.and[0].whenTrue");
        errors.ShouldContain(error =>
            error.Code == RuleErrorCode.UnknownSpec && error.Path == "$.rule.and[1]");
    }

    [Fact]
    public void Should_reject_object_payloads_nested_inside_a_remetadatized_async_subtree()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());
        const string document =
            """
            {
              "rule": {
                "and": [
                  { "spec": "credit-check" },
                  { "spec": "is-active",
                    "whenTrue": { "Code": "A" },
                    "whenFalse": { "Code": "B" },
                    "name": "inner" }
                ],
                "whenTrue": { "Code": "OUTER-T" },
                "whenFalse": { "Code": "OUTER-F" },
                "name": "outer"
              }
            }
            """;

        // Act
        var act = () => serializer.DeserializeAsyncSpec<Customer, Verdict>(document);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        exception.Errors.ShouldContain(error =>
            error.Code == RuleErrorCode.MetadataTypeMismatch && error.Path == "$.rule.and[1]");
    }

    [Fact]
    public async Task Should_remetadatize_an_async_leaf_with_non_string_registered_metadata()
    {
        // Arrange — 'credit-verdict' yields Verdict metadata, so the explanation subtree
        // underneath the payload decoration adapts it via ToAsyncExplanationSpec
        var serializer = new RuleSerializer(Registry());
        const string document =
            """
            {
              "rule": {
                "spec": "credit-verdict",
                "whenTrue": { "Code": "RE-OK" },
                "whenFalse": { "Code": "RE-BAD" },
                "name": "re-coded"
              }
            }
            """;

        // Act
        var spec = serializer.DeserializeAsyncSpec<Customer, Verdict>(document);

        // Assert
        var satisfied = await spec.EvaluateAsync(new Customer(true, 30));
        satisfied.Satisfied.ShouldBeTrue();
        satisfied.Values.ShouldBe([new Verdict("RE-OK")]);
        satisfied.Assertions.ShouldBe(["re-coded == true"]);
        var unsatisfied = await spec.EvaluateAsync(new Customer(true, 18));
        unsatisfied.Satisfied.ShouldBeFalse();
        unsatisfied.Values.ShouldBe([new Verdict("RE-BAD")]);
        unsatisfied.Assertions.ShouldBe(["re-coded == false"]);
    }

    [Fact]
    public void Should_report_a_metadata_type_mismatch_for_a_sync_entry()
    {
        // Arrange — 'is-active' yields string metadata, not Verdict
        var serializer = new RuleSerializer(Registry());

        // Act
        var act = () => serializer.DeserializeAsyncSpec<Customer, Verdict>("""{ "rule": { "spec": "is-active" } }""");

        // Assert
        var error = act.ShouldThrow<RuleSerializationException>().Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
        error.Message.ShouldContain("is-active");
        error.Message.ShouldContain("String");
        error.Message.ShouldContain("Verdict");
    }

    [Fact]
    public void Should_report_a_metadata_type_mismatch_for_an_async_entry()
    {
        // Arrange — 'credit-check' is async but yields string metadata, not Verdict
        var serializer = new RuleSerializer(Registry());

        // Act
        var act = () => serializer.DeserializeAsyncSpec<Customer, Verdict>("""{ "rule": { "spec": "credit-check" } }""");

        // Assert
        var error = act.ShouldThrow<RuleSerializationException>().Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
        error.Message.ShouldContain("credit-check");
        error.Message.ShouldContain("String");
        error.Message.ShouldContain("Verdict");
    }

    [Fact]
    public void Should_report_a_model_type_mismatch_for_an_async_entry()
    {
        // Arrange — 'other-model-async' is registered for string, not Customer
        var serializer = new RuleSerializer(Registry());

        // Act
        var act = () =>
            serializer.DeserializeAsyncSpec<Customer, Verdict>("""{ "rule": { "spec": "other-model-async" } }""");

        // Assert
        var errors = act.ShouldThrow<RuleSerializationException>().Errors;
        errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.ModelTypeMismatch);
    }

    private sealed class Order(decimal total)
    {
        public decimal Total { get; } = total;
    }

    private sealed class Account(params Order[] orders)
    {
        public IReadOnlyList<Order> Orders { get; } = orders;
    }

    private static SpecBase<Order, Verdict> OrderVerdict { get; } =
        Spec.Build((Order o) => o.Total >= 100m)
            .WhenTrue(new Verdict("LARGE")).WhenFalse(new Verdict("SMALL")).Create("order verdict");

    private static SpecRegistry HigherOrderRegistry() => new SpecRegistry()
        .Register("order-verdict", OrderVerdict)
        .Register("async-order-verdict",
            Spec.BuildAsync((Order _) => new ValueTask<bool>(true))
                .WhenTrue(new Verdict("CHECKED")).WhenFalse(new Verdict("UNCHECKED")).Create("order checked"))
        .RegisterCollection<Account, Order>("orders", a => a.Orders);

    // Covers all-large (satisfied), mixed and none-large (unsatisfied), and the empty collection.
    private static readonly Account[] AccountModels =
    [
        new(new Order(150m), new Order(200m)),
        new(new Order(150m), new Order(50m)),
        new(new Order(10m), new Order(20m)),
        new()
    ];

    [Fact]
    public async Task Should_bind_a_higher_order_node_identically_to_the_sync_metadata_load()
    {
        // Arrange — the whole higher-order subtree binds through the sync metadata binder and
        // lifts, so the async load must match the sync load of the same document exactly
        var serializer = new RuleSerializer(HigherOrderRegistry());
        const string document =
            """{ "rule": { "asAllSatisfied": { "spec": "order-verdict" }, "path": "orders", "name": "all large" } }""";

        // Act
        var asyncLoaded = serializer.DeserializeAsyncSpec<Account, Verdict>(document);
        var result = await asyncLoaded.EvaluateAsync(new Account(new Order(150m), new Order(200m)));

        // Assert
        result.Satisfied.ShouldBeTrue();
        await ShouldBehaveIdenticallyAsync(asyncLoaded, serializer.Deserialize<Account, Verdict>(document), AccountModels);
    }

    [Fact]
    public void Should_reject_async_specs_inside_higher_order_subtrees_in_a_metadata_load()
    {
        // Arrange
        var serializer = new RuleSerializer(HigherOrderRegistry());
        const string document =
            """{ "rule": { "asAllSatisfied": { "spec": "async-order-verdict" }, "path": "orders", "name": "all checked" } }""";

        // Act
        var act = () => serializer.DeserializeAsyncSpec<Account, Verdict>(document);

        // Assert
        var error = act.ShouldThrow<RuleSerializationException>().Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.AsyncSpecInHigherOrder);
        error.Message.ShouldContain("higher-order propositions evaluate synchronously");
    }
}
