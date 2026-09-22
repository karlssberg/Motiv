using static Motiv.Serialization.Tests.SpecAssertions;

namespace Motiv.Serialization.Tests;

public class AsyncRuleBinderTests
{
    // Plain class (not a record) so the net472 target compiles without an IsExternalInit polyfill.
    private sealed class Customer(bool isActive, int age)
    {
        public bool IsActive { get; } = isActive;
        public int Age { get; } = age;
    }

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive)
            .WhenTrue("customer is active").WhenFalse("customer is inactive").Create();

    private static SpecBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18)
            .WhenTrue("customer is an adult").WhenFalse("customer is a minor").Create();

    private static AsyncSpecBase<Customer, string> PassesCreditCheck { get; } =
        Spec.BuildAsync((Customer c) => new ValueTask<bool>(c.Age >= 21))
            .WhenTrue("passes credit check").WhenFalse("fails credit check").Create();

    private static SpecRegistry Registry() => new SpecRegistry()
        .Register("is-active", IsActive)
        .Register("is-adult", IsAdult)
        .Register("passes-credit-check", PassesCreditCheck)
        .Register("other-model-async",
            Spec.BuildAsync((string _) => new ValueTask<bool>(true))
                .WhenTrue("t").WhenFalse("f").Create());

    // Covers every combination of active/inactive with adult-and-creditworthy, adult-but-not-creditworthy, and minor.
    private static readonly Customer[] Models =
    [
        new(true, 30), new(false, 30), new(true, 18), new(false, 18), new(true, 10), new(false, 10)
    ];

    [Fact]
    public async Task Should_bind_an_async_leaf()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var spec = serializer.DeserializeAsyncSpec<Customer>("""{ "rule": { "spec": "passes-credit-check" } }""");
        var result = await spec.EvaluateAsync(new Customer(true, 30));

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["passes credit check"]);
    }

    [Fact]
    public async Task Should_bind_a_sync_leaf_by_lifting_it()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var spec = serializer.DeserializeAsyncSpec<Customer>("""{ "rule": { "spec": "is-active" } }""");
        var result = await spec.EvaluateAsync(new Customer(false, 30));

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Assertions.ShouldBe(["customer is inactive"]);
    }

    public static TheoryData<string, string> BinaryOperators => new()
    {
        { "and", "And" },
        { "or", "Or" },
        { "xor", "XOr" },
        { "andAlso", "AndAlso" },
        { "orElse", "OrElse" }
    };

    private static AsyncSpecBase<Customer, string> Compose(
        string method,
        AsyncSpecBase<Customer, string> left,
        AsyncSpecBase<Customer, string> right) =>
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
    public async Task Should_bind_each_binary_operator_over_the_async_boundary_to_its_fluent_equivalent(
        string jsonOperator, string method)
    {
        // Arrange — sync left is lifted, async right is used directly
        var json =
            $$"""{ "rule": { "{{jsonOperator}}": [ { "spec": "is-active" }, { "spec": "passes-credit-check" } ] } }""";
        var expected = Compose(method, IsActive.ToAsyncSpec(), PassesCreditCheck);

        // Act
        var loaded = new RuleSerializer(Registry()).DeserializeAsyncSpec<Customer>(json);

        // Assert
        await ShouldBehaveIdenticallyAsync(loaded, expected, Models);
    }

    [Fact]
    public async Task Should_denoise_a_single_causal_operand_like_the_sync_load()
    {
        // Arrange — a fully-sync document loaded async must match the sync load exactly,
        // including causal filtering when only one 'or' operand influences the outcome.
        var serializer = new RuleSerializer(Registry());
        var document = """{ "rule": { "or": [ { "spec": "is-active" }, { "spec": "is-adult" } ] } }""";

        // Act
        var syncLoaded = serializer.Deserialize<Customer>(document);
        var asyncLoaded = serializer.DeserializeAsyncSpec<Customer>(document);

        // Assert — Customer(true, 10) satisfies only 'is-active', so it is the sole causal operand
        var result = await asyncLoaded.EvaluateAsync(new Customer(true, 10));
        result.Assertions.ShouldBe(["customer is active"]);
        await ShouldBehaveIdenticallyAsync(asyncLoaded, syncLoaded, Models);
    }

    [Fact]
    public async Task Should_fold_arrays_of_more_than_two_operands_to_the_left()
    {
        // Arrange
        var json =
            """
            { "rule": { "and": [ { "spec": "is-active" }, { "spec": "is-adult" }, { "spec": "passes-credit-check" } ] } }
            """;
        var expected = IsActive.ToAsyncSpec().And(IsAdult.ToAsyncSpec()).And(PassesCreditCheck);

        // Act
        var loaded = new RuleSerializer(Registry()).DeserializeAsyncSpec<Customer>(json);

        // Assert
        await ShouldBehaveIdenticallyAsync(loaded, expected, Models);
    }

    [Fact]
    public async Task Should_short_circuit_or_else_across_the_async_boundary()
    {
        // Arrange — orElse with a satisfied sync left must not invoke the async right
        var calls = 0;
        var registry = new SpecRegistry()
            .Register("always-true",
                Spec.Build((Customer _) => true).WhenTrue("t").WhenFalse("f").Create())
            .Register("expensive",
                Spec.BuildAsync((Customer _) => { calls++; return new ValueTask<bool>(true); })
                    .WhenTrue("t2").WhenFalse("f2").Create());
        var serializer = new RuleSerializer(registry);
        var document = """{ "rule": { "orElse": [ { "spec": "always-true" }, { "spec": "expensive" } ] } }""";

        // Act
        var result = await serializer.DeserializeAsyncSpec<Customer>(document).EvaluateAsync(new Customer(true, 1));

        // Assert
        result.Satisfied.ShouldBeTrue();
        calls.ShouldBe(0);
    }

    [Fact]
    public async Task Should_bind_not_nodes()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var spec = serializer.DeserializeAsyncSpec<Customer>(
            """{ "rule": { "not": { "spec": "passes-credit-check" } } }""");
        var result = await spec.EvaluateAsync(new Customer(true, 30));

        // Assert
        result.Satisfied.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_decorate_an_async_leaf_like_a_fluent_explanation_wrapper()
    {
        // Arrange
        var json =
            """{ "rule": { "spec": "passes-credit-check", "whenTrue": "ok", "whenFalse": "bad" } }""";
        var expected = Spec.Build(PassesCreditCheck).WhenTrue("ok").WhenFalse("bad").Create();

        // Act
        var loaded = new RuleSerializer(Registry()).DeserializeAsyncSpec<Customer>(json);

        // Assert
        var satisfied = await loaded.EvaluateAsync(new Customer(true, 30));
        satisfied.Assertions.ShouldBe(["ok"]);
        var unsatisfied = await loaded.EvaluateAsync(new Customer(true, 18));
        unsatisfied.Assertions.ShouldBe(["bad"]);
        await ShouldBehaveIdenticallyAsync(loaded, expected, Models);
    }

    [Fact]
    public async Task Should_decorate_a_named_async_leaf_like_a_named_fluent_wrapper()
    {
        // Arrange
        var json =
            """{ "rule": { "spec": "passes-credit-check", "whenTrue": "ok", "whenFalse": "bad", "name": "check" } }""";
        var expected = Spec.Build(PassesCreditCheck).WhenTrue("ok").WhenFalse("bad").Create("check");

        // Act
        var loaded = new RuleSerializer(Registry()).DeserializeAsyncSpec<Customer>(json);

        // Assert
        var result = await loaded.EvaluateAsync(new Customer(true, 30));
        result.Assertions.ShouldBe(["check == true"]);
        await ShouldBehaveIdenticallyAsync(loaded, expected, Models);
    }

    [Fact]
    public async Task Should_wrap_a_name_only_node_like_a_fluent_create_wrapper()
    {
        // Arrange
        var json = """{ "rule": { "spec": "passes-credit-check", "name": "wrapper" } }""";
        var expected = Spec.Build(PassesCreditCheck).Create("wrapper");

        // Act
        var loaded = new RuleSerializer(Registry()).DeserializeAsyncSpec<Customer>(json);

        // Assert — the statement is renamed, so Reason takes the new name, while the
        // underlying assertion still surfaces
        var result = await loaded.EvaluateAsync(new Customer(true, 30));
        result.Reason.ShouldBe("wrapper == true");
        result.Assertions.ShouldBe(["passes credit check"]);
        await ShouldBehaveIdenticallyAsync(loaded, expected, Models);
    }

    [Fact]
    public async Task Should_wrap_the_root_with_the_document_name()
    {
        // Arrange
        var json = """{ "name": "document rule", "rule": { "spec": "passes-credit-check" } }""";
        var expected = Spec.Build(PassesCreditCheck).Create("document rule");

        // Act
        var loaded = new RuleSerializer(Registry()).DeserializeAsyncSpec<Customer>(json);

        // Assert — the document name renames the statement, so Reason takes the name, while
        // the underlying assertion still surfaces
        var result = await loaded.EvaluateAsync(new Customer(true, 30));
        result.Reason.ShouldBe("document rule == true");
        result.Assertions.ShouldBe(["passes credit check"]);
        await ShouldBehaveIdenticallyAsync(loaded, expected, Models);
    }

    [Fact]
    public void Should_report_unknown_specs_with_the_same_error_as_the_sync_binder()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var act = () => serializer.DeserializeAsyncSpec<Customer>("""{ "rule": { "spec": "missing" } }""");

        // Assert
        var errors = act.ShouldThrow<RuleSerializationException>().Errors;
        errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.UnknownSpec);
    }

    [Fact]
    public void Should_report_model_type_mismatches()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act — loading a Customer-spec document for int
        var act = () => serializer.DeserializeAsyncSpec<int>("""{ "rule": { "spec": "is-active" } }""");

        // Assert
        var errors = act.ShouldThrow<RuleSerializationException>().Errors;
        errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.ModelTypeMismatch);
    }

    [Fact]
    public void Should_report_model_type_mismatches_for_async_entries()
    {
        // Arrange — 'other-model-async' is registered for string, not Customer
        var serializer = new RuleSerializer(Registry());

        // Act
        var act = () => serializer.DeserializeAsyncSpec<Customer>("""{ "rule": { "spec": "other-model-async" } }""");

        // Assert
        var errors = act.ShouldThrow<RuleSerializationException>().Errors;
        errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.ModelTypeMismatch);
    }

    [Fact]
    public void Should_collect_errors_from_every_operand()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());
        var json = """{ "rule": { "and": [ { "spec": "missing-1" }, { "spec": "missing-2" } ] } }""";

        // Act
        var act = () => serializer.DeserializeAsyncSpec<Customer>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        exception.Errors.Count.ShouldBe(2);
        exception.Errors.ShouldAllBe(error => error.Code == RuleErrorCode.UnknownSpec);
    }

    private sealed class Order(decimal total)
    {
        public decimal Total { get; } = total;
    }

    private sealed class Account(params Order[] orders)
    {
        public IReadOnlyList<Order> Orders { get; } = orders;
    }

    private static SpecBase<Order, string> IsLargeOrder { get; } =
        Spec.Build((Order o) => o.Total >= 100m)
            .WhenTrue("order is large").WhenFalse("order is small").Create();

    private static AsyncSpecBase<Account, string> AsyncAccountCheck { get; } =
        Spec.BuildAsync((Account a) => new ValueTask<bool>(a.Orders.Count > 0))
            .WhenTrue("account has orders").WhenFalse("account has no orders").Create();

    private static SpecRegistry HigherOrderRegistry() => new SpecRegistry()
        .Register("is-large-order", IsLargeOrder)
        .Register("async-order-check",
            Spec.BuildAsync((Order _) => new ValueTask<bool>(true))
                .WhenTrue("checked").WhenFalse("unchecked").Create())
        .Register("async-account-check", AsyncAccountCheck)
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
    public async Task Should_bind_a_fully_sync_higher_order_subtree_by_lifting_it()
    {
        // Arrange
        var serializer = new RuleSerializer(HigherOrderRegistry());
        const string document =
            """{ "rule": { "asAllSatisfied": { "spec": "is-large-order" }, "path": "orders", "name": "all orders large" } }""";

        // Act
        var asyncLoaded = serializer.DeserializeAsyncSpec<Account>(document);
        var result = await asyncLoaded.EvaluateAsync(new Account(new Order(150m), new Order(200m)));

        // Assert — the subtree binds through the sync binder and lifts, so the async load
        // must match the sync load of the same document exactly
        result.Satisfied.ShouldBeTrue();
        await ShouldBehaveIdenticallyAsync(asyncLoaded, serializer.Deserialize<Account>(document), AccountModels);
    }

    [Fact]
    public async Task Should_bind_a_decorated_higher_order_node_with_sync_load_parity()
    {
        // Arrange — the sync binder applies the quantifier node's decorations for the whole
        // subtree. This pins the decorated-node parity contract only: a second decoration after
        // the lift is observationally collapsed by Motiv and costs just a redundant wrapper
        // allocation, so the BindNode early-return is an efficiency/cleanliness invariant
        // enforced by inspection rather than by this test.
        var serializer = new RuleSerializer(HigherOrderRegistry());
        const string document =
            """
            {
              "rule": {
                "asAllSatisfied": { "spec": "is-large-order" },
                "path": "orders",
                "whenTrue": "every order is large",
                "whenFalse": "an order is small",
                "name": "order sizes"
              }
            }
            """;

        // Act
        var asyncLoaded = serializer.DeserializeAsyncSpec<Account>(document);

        // Assert
        var result = await asyncLoaded.EvaluateAsync(new Account(new Order(150m), new Order(50m)));
        result.Satisfied.ShouldBeFalse();
        result.Reason.ShouldBe("order sizes == false");
        await ShouldBehaveIdenticallyAsync(asyncLoaded, serializer.Deserialize<Account>(document), AccountModels);
    }

    [Fact]
    public async Task Should_compose_a_higher_order_operand_with_an_async_operand()
    {
        // Arrange — a higher-order node as a composition operand exercises the
        // BindComposition → BindNode → higher-order branch edge of the async binder.
        const string json =
            """
            {
              "rule": {
                "and": [
                  { "asAllSatisfied": { "spec": "is-large-order" }, "path": "orders", "name": "all orders large" },
                  { "spec": "async-account-check" }
                ]
              }
            }
            """;
        var quantifier = Spec.Build(IsLargeOrder).AsAllSatisfied()
            .WhenTrue("all satisfied").WhenFalse("not all satisfied").Create()
            .ChangeModelTo<Account>(a => a.Orders);
        var expected = Spec.Build(quantifier).Create("all orders large").ToAsyncSpec().And(AsyncAccountCheck);

        // Act
        var loaded = new RuleSerializer(HigherOrderRegistry()).DeserializeAsyncSpec<Account>(json);

        // Assert
        await ShouldBehaveIdenticallyAsync(loaded, expected, AccountModels);
    }

    [Fact]
    public void Should_reject_async_specs_inside_higher_order_subtrees()
    {
        // Arrange
        var serializer = new RuleSerializer(HigherOrderRegistry());
        const string document =
            """{ "rule": { "asAllSatisfied": { "spec": "async-order-check" }, "path": "orders", "name": "all checked" } }""";

        // Act
        var act = () => serializer.DeserializeAsyncSpec<Account>(document);

        // Assert
        var errors = act.ShouldThrow<RuleSerializationException>().Errors;
        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.AsyncSpecInHigherOrder);
        error.Message.ShouldContain("higher-order propositions evaluate synchronously");
    }
}
