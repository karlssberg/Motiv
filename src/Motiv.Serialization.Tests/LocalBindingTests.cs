using static Motiv.Serialization.Tests.SpecAssertions;

namespace Motiv.Serialization.Tests;

/// <summary>
/// Task 7: the four binders resolve a <c>{ "local": "&lt;name&gt;" }</c> node by binding the
/// definition it names. Fixtures are shared across the sync, async, metadata and async-metadata
/// cases below, so they live in one file rather than being duplicated per binder.
/// </summary>
public class LocalBindingTests
{
    // Plain classes (not records) so the net472 target compiles without an IsExternalInit polyfill.
    private sealed class Order(decimal total)
    {
        public decimal Total { get; } = total;
    }

    private sealed class Customer(bool isActive, params Order[] orders)
    {
        public bool IsActive { get; } = isActive;
        public IReadOnlyList<Order> Orders { get; } = orders;
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
        Spec.BuildAsync((Customer c) => new ValueTask<bool>(c.Orders.Count > 0))
            .WhenTrue("passes credit check").WhenFalse("fails credit check").Create();

    private static SpecBase<Order, string> IsLargeOrder { get; } =
        Spec.Build((Order o) => o.Total >= 100m)
            .WhenTrue("order is large").WhenFalse("order is small").Create();

    private static SpecRegistry Registry() => new SpecRegistry()
        .Register("customer.is-active", IsActive)
        .Register("customer.active-verdict", ActiveVerdict)
        .Register("customer.credit-check", PassesCreditCheck)
        .Register("is-large-order", IsLargeOrder)
        .RegisterCollection<Customer, Order>("orders", c => c.Orders);

    // Covers active-with-orders, inactive-with-a-small-order, and active-with-no-orders.
    private static readonly Customer[] Models =
    [
        new(true, new Order(150m), new Order(200m)),
        new(false, new Order(10m)),
        new(true)
    ];

    [Fact]
    public void Should_validate_a_document_with_a_local_reference_without_throwing()
    {
        // Arrange — the Task 6 review defect: a valid local document parses with zero structural
        // errors, and BindComposition's Aggregate over an empty sequence used to throw
        // InvalidOperationException before any Local arm existed.
        const string json =
            """{ "definitions": { "d": { "rule": { "spec": "customer.is-active" } } }, "rule": { "local": "d" } }""";

        // Act
        var errors = new RuleSerializer(Registry()).Validate<Customer>(json);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Should_bind_a_local_through_its_definition()
    {
        // Arrange
        const string json =
            """
            { "definitions": { "is-active": { "rule": { "spec": "customer.is-active" } } },
              "rule": { "local": "is-active" } }
            """;
        var expected = Spec.Build(IsActive).Create("is-active");

        // Act
        var loaded = new RuleSerializer(Registry()).Deserialize<Customer>(json);

        // Assert — the definition's key names the statement, so Reason takes the suffixed form
        // while the underlying assertion still surfaces
        var result = loaded.Evaluate(new Customer(true));
        result.Reason.ShouldBe("is-active == true");
        result.Assertions.ShouldBe(["customer is active"]);
        ShouldBehaveIdentically(loaded, expected, Models);
    }

    [Fact]
    public void Should_bind_a_local_definition_with_whenTrue_and_whenFalse()
    {
        // Arrange
        const string json =
            """
            { "definitions": {
                "active": {
                  "rule": { "spec": "customer.is-active" },
                  "whenTrue": "yes, active",
                  "whenFalse": "not active"
                }
              },
              "rule": { "local": "active" } }
            """;
        // A definition's body always carries its key as the node's name, so this is the "named
        // explanation proposition" pattern: the strings are demoted to Values, and the name +
        // suffix becomes the assertion text — never the unnamed "strings-are-the-assertions" form.
        var expected = Spec.Build(IsActive).WhenTrue("yes, active").WhenFalse("not active").Create("active");

        // Act
        var loaded = new RuleSerializer(Registry()).Deserialize<Customer>(json);

        // Assert
        var result = loaded.Evaluate(new Customer(true));
        result.Reason.ShouldBe("active == true");
        result.Assertions.ShouldBe(["active == true"]);
        ShouldBehaveIdentically(loaded, expected, Models);
    }

    [Fact]
    public void Should_behave_identically_when_the_same_local_is_referenced_twice_under_and()
    {
        // Arrange
        const string json =
            """
            { "definitions": { "active": { "rule": { "spec": "customer.is-active" } } },
              "rule": { "and": [ { "local": "active" }, { "local": "active" } ] } }
            """;
        var d = Spec.Build(IsActive).Create("active");
        var expected = d & d;

        // Act
        var loaded = new RuleSerializer(Registry()).Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, Models);
    }

    [Fact]
    public void Should_bind_a_local_inside_a_quantifier_against_the_element_model()
    {
        // Arrange — the definition body references an Order spec; bound from inside
        // asAllSatisfied/orders, its model is the element type, not the document root's Customer
        const string json =
            """
            { "definitions": { "large": { "rule": { "spec": "is-large-order" } } },
              "rule": { "asAllSatisfied": { "local": "large" }, "path": "orders", "name": "all large" } }
            """;
        var inner = Spec.Build(IsLargeOrder).Create("large");
        var quantifier = Spec.Build(inner).AsAllSatisfied()
            .WhenTrue("all satisfied").WhenFalse("not all satisfied").Create()
            .ChangeModelTo<Customer>(c => c.Orders);
        var expected = Spec.Build(quantifier).Create("all large");

        // Act
        var loaded = new RuleSerializer(Registry()).Deserialize<Customer>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, Models);
    }

    [Fact]
    public void Should_report_a_model_type_mismatch_when_the_same_local_is_referenced_at_the_document_root()
    {
        // Arrange — the same definition body (an Order spec) resolves fine inside the quantifier
        // above, but referencing it directly from a Customer-typed document root is a genuine
        // model mismatch. The error is reported at the spec leaf's path inside the definition body,
        // since a local is bound by binding its definition at the reference's model.
        const string json =
            """{ "definitions": { "large": { "rule": { "spec": "is-large-order" } } }, "rule": { "local": "large" } }""";

        // Act
        var act = () => new RuleSerializer(Registry()).Deserialize<Customer>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ModelTypeMismatch);
        error.Path.ShouldBe("$.definitions.large.rule");
    }

    [Fact]
    public async Task Should_bind_a_local_through_its_definition_over_the_async_boundary()
    {
        // Arrange — a sync-registered leaf inside the definition, lifted through an async load
        const string json =
            """
            { "definitions": { "active": { "rule": { "spec": "customer.is-active" } } },
              "rule": { "local": "active" } }
            """;
        var expected = Spec.Build(IsActive).Create("active").ToAsyncSpec();

        // Act
        var loaded = new RuleSerializer(Registry()).DeserializeAsyncSpec<Customer>(json);

        // Assert
        var result = await loaded.EvaluateAsync(new Customer(true));
        result.Reason.ShouldBe("active == true");
        await ShouldBehaveIdenticallyAsync(loaded, expected, Models);
    }

    [Fact]
    public async Task Should_bind_a_local_naming_an_async_leaf()
    {
        // Arrange
        const string json =
            """
            { "definitions": { "credit": { "rule": { "spec": "customer.credit-check" } } },
              "rule": { "local": "credit" } }
            """;
        var expected = Spec.Build(PassesCreditCheck).Create("credit");

        // Act
        var loaded = new RuleSerializer(Registry()).DeserializeAsyncSpec<Customer>(json);

        // Assert
        await ShouldBehaveIdenticallyAsync(loaded, expected, Models);
    }

    [Fact]
    public void Should_bind_a_local_definition_with_an_object_payload_in_a_metadata_load()
    {
        // Arrange
        const string json =
            """
            { "definitions": { "verdict": { "rule": { "spec": "customer.is-active" },
                                             "whenTrue": { "Code": "OK" }, "whenFalse": { "Code": "NO" } } },
              "rule": { "local": "verdict" } }
            """;
        var expected = Spec.Build(IsActive)
            .WhenTrue(new Verdict("OK")).WhenFalse(new Verdict("NO")).Create("verdict");

        // Act
        var loaded = new RuleSerializer(Registry()).Deserialize<Customer, Verdict>(json);

        // Assert
        var result = loaded.Evaluate(new Customer(true));
        result.Assertions.ShouldBe(["verdict == true"]);
        result.Values.ShouldBe([new Verdict("OK")]);
        ShouldBehaveIdentically(loaded, expected, Models);
    }

    [Fact]
    public void Should_bind_a_local_naming_a_pre_typed_metadata_leaf()
    {
        // Arrange
        const string json =
            """
            { "definitions": { "v": { "rule": { "spec": "customer.active-verdict" } } },
              "rule": { "local": "v" } }
            """;
        // The definition's key ("v") always becomes the body's name, renaming the already-typed
        // metadata leaf exactly as Spec.Build(spec).Create(name) does in core.
        var expected = Spec.Build(ActiveVerdict).Create("v");

        // Act
        var loaded = new RuleSerializer(Registry()).Deserialize<Customer, Verdict>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, Models);
    }

    [Fact]
    public async Task Should_bind_a_local_definition_with_an_object_payload_in_an_async_metadata_load()
    {
        // Arrange
        const string json =
            """
            { "definitions": { "verdict": { "rule": { "spec": "customer.is-active" },
                                             "whenTrue": { "Code": "OK" }, "whenFalse": { "Code": "NO" } } },
              "rule": { "local": "verdict" } }
            """;
        var expected = Spec.Build(IsActive)
            .WhenTrue(new Verdict("OK")).WhenFalse(new Verdict("NO")).Create("verdict");

        // Act
        var loaded = new RuleSerializer(Registry()).DeserializeAsyncSpec<Customer, Verdict>(json);

        // Assert
        var result = await loaded.EvaluateAsync(new Customer(true));
        result.Assertions.ShouldBe(["verdict == true"]);
        result.Values.ShouldBe([new Verdict("OK")]);
        await ShouldBehaveIdenticallyAsync(loaded, expected, Models);
    }
}
