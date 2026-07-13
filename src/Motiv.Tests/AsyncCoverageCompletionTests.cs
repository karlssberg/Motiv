using Motiv.And;
using Motiv.AndAlso;
using Motiv.MetadataToExplanationAdapter;
using Motiv.Not;
using Motiv.Or;
using Motiv.OrElse;
using Motiv.Shared;
using Motiv.SyncToAsyncAdapter;
using Motiv.Traversal;
using Motiv.XOr;

namespace Motiv.Tests;

/// <summary>
/// Closes the codecov patch gap for the async-propositions feature by exercising leaf members (constructors,
/// <c>Underlying</c>, <c>Description</c>, explicit interface implementations, <c>MatchesAsync</c> overrides) that
/// the primary async test suites don't happen to hit while asserting richer behaviour. Depth is owned by those
/// suites — this file intentionally stays broad and shallow, one region per source file under test.
/// </summary>
public class AsyncCoverageCompletionTests
{
    #region AsyncSpec.cs — opposite constructor forms + null guards

    private sealed class DirectCtorGrade(AsyncSpecBase<int, char> spec) : AsyncSpec<int, char>(spec);

    private sealed class FactoryCtorIsPositive(Func<AsyncSpecBase<int, string>> factory) : AsyncSpec<int>(factory);

    [Fact]
    public async Task Should_construct_typed_AsyncSpec_via_the_direct_spec_constructor()
    {
        // Arrange
        var underlying = Spec.BuildAsync((int n) => Task.FromResult(n >= 50))
            .WhenTrue('P').WhenFalse('F').Create("passing grade");

        // Act
        var spec = new DirectCtorGrade(underlying);
        var result = await spec.EvaluateAsync(80);

        // Assert
        result.Values.ShouldBe(['P']);
        spec.Underlying.ShouldBe(underlying.Underlying);
        spec.Description.ShouldBeSameAs(underlying.Description);
        (await spec.MatchesAsync(80)).ShouldBeTrue();
    }

    [Fact]
    public void Should_guard_the_direct_spec_constructor_against_null()
    {
        // Act
        var act = () => new DirectCtorGrade(null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public async Task Should_construct_untyped_AsyncSpec_via_the_factory_constructor()
    {
        // Arrange
        AsyncSpecBase<int, string> Factory() => Spec.BuildAsync((int n) => Task.FromResult(n > 0))
            .WhenTrue("is positive").WhenFalse("is not positive").Create();

        // Act
        var spec = new FactoryCtorIsPositive(Factory);
        var result = await spec.EvaluateAsync(1);

        // Assert
        result.Satisfied.ShouldBeTrue();
        spec.Underlying.ShouldBeEmpty();
        (await spec.MatchesAsync(1)).ShouldBeTrue();
        spec.Description.ShouldNotBeNull();
    }

    [Fact]
    public void Should_guard_the_factory_constructor_against_a_null_factory()
    {
        // Act
        var act = () => new FactoryCtorIsPositive(null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Should_guard_the_factory_constructor_against_a_null_factory_output()
    {
        // Act
        var act = () => new FactoryCtorIsPositive(() => null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    #endregion

    #region AsyncSpecBase.cs — default MatchesAsync impls + typed/sync mixed overloads

    private sealed class UntypedPassthrough(AsyncSpecBase<int, string> inner) : AsyncSpecBase<int>
    {
        public override IEnumerable<SpecBase> Underlying => inner.Underlying;
        public override ISpecDescription Description => inner.Description;
        public override AsyncSpecBase<int, string> ToAsyncExplanationSpec() => inner;
    }

    private sealed class TypedPassthrough(AsyncSpecBase<int, string> inner) : AsyncSpecBase<int, string>
    {
        public override IEnumerable<SpecBase> Underlying => inner.Underlying;
        public override ISpecDescription Description => inner.Description;

        protected override Task<BooleanResultBase<string>> EvaluateSpecAsync(
            int model, CancellationToken cancellationToken) =>
            inner.EvaluateAsync(model, cancellationToken);
    }

    [Fact]
    public async Task Should_use_the_default_untyped_MatchesAsync_implementation()
    {
        // Arrange
        var inner = Spec.BuildAsync((int n) => Task.FromResult(n > 0))
            .WhenTrue("positive").WhenFalse("not positive").Create();
        AsyncSpecBase<int> spec = new UntypedPassthrough(inner);

        // Act
        var matches = await spec.MatchesAsync(1);

        // Assert
        matches.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_use_the_default_typed_MatchesAsync_implementation()
    {
        // Arrange
        var inner = Spec.BuildAsync((int n) => Task.FromResult(n > 0))
            .WhenTrue("positive").WhenFalse("not positive").Create();
        AsyncSpecBase<int, string> spec = new TypedPassthrough(inner);

        // Act
        var matches = await spec.MatchesAsync(1);

        // Assert
        matches.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_invoke_typed_mixed_operand_overloads_on_AsyncSpecBase()
    {
        // Arrange — same TMetadata on both sides so the typed (not untyped) overloads bind
        AsyncSpecBase<int, string> asyncLeftTrue = Spec.BuildAsync((int _) => Task.FromResult(true))
            .WhenTrue("yes").WhenFalse("no").Create("left-true");
        AsyncSpecBase<int, string> asyncLeftFalse = Spec.BuildAsync((int _) => Task.FromResult(false))
            .WhenTrue("yes").WhenFalse("no").Create("left-false");
        SpecBase<int, string> syncRight = Spec.Build((int _) => true)
            .WhenTrue("yes").WhenFalse("no").Create("right");

        // Act
        var andAlso = await asyncLeftTrue.AndAlso(syncRight).EvaluateAsync(1);
        var orElse = await asyncLeftFalse.OrElse(syncRight).EvaluateAsync(1);
        var andConcurrently = await asyncLeftTrue.AndConcurrently(syncRight).EvaluateAsync(1);
        var orConcurrently = await asyncLeftTrue.OrConcurrently(syncRight).EvaluateAsync(1);
        var xorConcurrently = await asyncLeftFalse.XOrConcurrently(syncRight).EvaluateAsync(1);

        // Assert
        andAlso.Satisfied.ShouldBeTrue();
        orElse.Satisfied.ShouldBeTrue();
        andConcurrently.Satisfied.ShouldBeTrue();
        orConcurrently.Satisfied.ShouldBeTrue();
        xorConcurrently.Satisfied.ShouldBeTrue();
    }

    #endregion

    #region AsyncSpecExtensions.cs — sync-composite collapse arm inside a mixed tree

    [Fact]
    public async Task Should_flatten_a_lifted_sync_composite_operand_when_rendering_justification()
    {
        // Arrange — a native sync AND composite (itself collapsible) lifted wholesale as the async right operand
        var syncA = Spec.Build((int _) => true).Create("syncA");
        var syncB = Spec.Build((int _) => true).Create("syncB");
        var syncComposite = syncA.And(syncB);
        var asyncLeft = Spec.BuildAsync((int _) => Task.FromResult(true)).Create("asyncLeft");

        // Act — the composite's own description drives the collapsing traversal via GetMixedBinaryJustificationAsLines
        var composed = asyncLeft.And(syncComposite);
        var detailed = composed.Description.Detailed;
        var result = await composed.EvaluateAsync(1);

        // Assert — the flattened sync composite contributes its own leaf lines, not a nested group
        result.Satisfied.ShouldBeTrue();
        detailed.ShouldContain("syncA");
        detailed.ShouldContain("syncB");
    }

    #endregion

    #region AsyncXOrSpec.cs / AsyncAndSpec.cs / AsyncOrSpec.cs / AsyncAndAlsoSpec.cs / AsyncOrElseSpec.cs — traversal surface

    [Fact]
    public void Should_expose_the_binary_traversal_surface_on_AsyncXOrSpec()
    {
        // Arrange
        var left = Spec.BuildAsync((int _) => Task.FromResult(true)).Create("left");
        var right = Spec.BuildAsync((int _) => Task.FromResult(false)).Create("right");

        // Act
        var spec = (AsyncXOrSpec<int, string>)left.XOr(right);
        var typed = (IAsyncBinaryOperationSpec<int, string>)spec;
        var untyped = (IAsyncBinaryOperationSpec)spec;

        // Assert
        spec.Underlying.ShouldBe([left, right]);
        typed.Operation.ShouldBe(Operator.XOr);
        typed.IsCollapsable.ShouldBeFalse();
        typed.Left.ShouldBeSameAs(left);
        typed.Right.ShouldBeSameAs(right);
        untyped.Left.ShouldBeSameAs(left);
        untyped.Right.ShouldBeSameAs(right);
    }

    [Fact]
    public void Should_expose_the_binary_traversal_surface_on_AsyncAndSpec()
    {
        // Arrange
        var left = Spec.BuildAsync((int _) => Task.FromResult(true)).Create("left");
        var right = Spec.BuildAsync((int _) => Task.FromResult(true)).Create("right");

        // Act
        var spec = (AsyncAndSpec<int, string>)left.And(right);
        var untyped = (IAsyncBinaryOperationSpec)spec;

        // Assert
        spec.Underlying.ShouldBe([left, right]);
        untyped.Left.ShouldBeSameAs(left);
        untyped.Right.ShouldBeSameAs(right);
    }

    [Fact]
    public void Should_expose_the_binary_traversal_surface_on_AsyncOrSpec()
    {
        // Arrange
        var left = Spec.BuildAsync((int _) => Task.FromResult(false)).Create("left");
        var right = Spec.BuildAsync((int _) => Task.FromResult(true)).Create("right");

        // Act
        var spec = (AsyncOrSpec<int, string>)left.Or(right);
        var untyped = (IAsyncBinaryOperationSpec)spec;

        // Assert
        spec.Underlying.ShouldBe([left, right]);
        untyped.Left.ShouldBeSameAs(left);
        untyped.Right.ShouldBeSameAs(right);
    }

    [Fact]
    public void Should_expose_the_binary_traversal_surface_on_AsyncAndAlsoSpec()
    {
        // Arrange
        var left = Spec.BuildAsync((int _) => Task.FromResult(true)).Create("left");
        var right = Spec.BuildAsync((int _) => Task.FromResult(true)).Create("right");

        // Act
        var spec = (AsyncAndAlsoSpec<int, string>)left.AndAlso(right);
        var typed = (IAsyncBinaryOperationSpec<int, string>)spec;
        var untyped = (IAsyncBinaryOperationSpec)spec;

        // Assert
        spec.Underlying.ShouldBe([left, right]);
        typed.Operation.ShouldBe(Operator.AndAlso);
        typed.IsCollapsable.ShouldBeTrue();
        typed.Left.ShouldBeSameAs(left);
        typed.Right.ShouldBeSameAs(right);
        untyped.Left.ShouldBeSameAs(left);
        untyped.Right.ShouldBeSameAs(right);
    }

    [Fact]
    public void Should_expose_the_binary_traversal_surface_on_AsyncOrElseSpec()
    {
        // Arrange — explicitly typed as AsyncSpecBase (not AsyncPolicyBase) so OrElse resolves to the
        // Spec-producing overload rather than the policy-preserving one
        AsyncSpecBase<int, string> left = Spec.BuildAsync((int _) => Task.FromResult(false)).Create("left");
        AsyncSpecBase<int, string> right = Spec.BuildAsync((int _) => Task.FromResult(true)).Create("right");

        // Act
        var spec = (AsyncOrElseSpec<int, string>)left.OrElse(right);
        var typed = (IAsyncBinaryOperationSpec<int, string>)spec;
        var untyped = (IAsyncBinaryOperationSpec)spec;

        // Assert
        spec.Underlying.ShouldBe([left, right]);
        typed.Operation.ShouldBe(Operator.OrElse);
        typed.IsCollapsable.ShouldBeTrue();
        typed.Left.ShouldBeSameAs(left);
        typed.Right.ShouldBeSameAs(right);
        untyped.Left.ShouldBeSameAs(left);
        untyped.Right.ShouldBeSameAs(right);
    }

    #endregion

    #region AsyncOrElsePolicy.cs — Description family predicate, traversal surface, MatchesAsync

    [Fact]
    public async Task Should_expose_the_full_surface_of_AsyncOrElsePolicy()
    {
        // Arrange
        AsyncPolicyBase<int, string> primary = Spec.BuildAsync((int _) => Task.FromResult(false))
            .WhenTrue("yes").WhenFalse("no").Create("primary");
        AsyncPolicyBase<int, string> fallback = Spec.BuildAsync((int _) => Task.FromResult(true))
            .WhenTrue("yes").WhenFalse("no").Create("fallback");

        // Act
        var policy = (AsyncOrElsePolicy<int, string>)primary.OrElse(fallback);
        var typed = (IAsyncBinaryOperationSpec<int, string>)policy;
        var untyped = (IAsyncBinaryOperationSpec)policy;
        var statement = policy.Description.Statement; // forces the isSameFamily lambda (Summarize)
        var matches = await policy.MatchesAsync(1);

        // Assert
        policy.Underlying.ShouldBe([primary, fallback]);
        typed.Operation.ShouldBe(Operator.OrElse);
        typed.IsCollapsable.ShouldBeTrue();
        typed.Left.ShouldBeSameAs(primary);
        typed.Right.ShouldBeSameAs(fallback);
        untyped.Left.ShouldBeSameAs(primary);
        untyped.Right.ShouldBeSameAs(fallback);
        statement.ShouldContain("primary");
        statement.ShouldContain("fallback");
        matches.ShouldBeTrue();
    }

    #endregion

    #region AsyncNotSpec.cs / AsyncNotPolicy.cs — traversal surface + direct MatchesAsync

    [Fact]
    public async Task Should_expose_the_unary_traversal_surface_on_AsyncNotSpec()
    {
        // Arrange — explicitly typed as AsyncSpecBase (not AsyncPolicyBase) so Not() resolves to the
        // Spec-producing overload rather than the policy-preserving one
        AsyncSpecBase<int, string> operand = Spec.BuildAsync((int _) => Task.FromResult(true))
            .WhenTrue("yes").WhenFalse("no").Create("operand");

        // Act
        var spec = (AsyncNotSpec<int, string>)operand.Not();
        var unary = (IAsyncUnaryOperationSpec)spec;
        var matches = await spec.MatchesAsync(1);

        // Assert
        spec.Underlying.ShouldBe([operand]);
        unary.Operand.ShouldBeSameAs(operand);
        unary.Operation.ShouldBe(Operator.Not);
        unary.IsCollapsable.ShouldBeFalse();
        matches.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_expose_the_unary_traversal_surface_on_AsyncNotPolicy()
    {
        // Arrange
        AsyncPolicyBase<int, string> operand = Spec.BuildAsync((int _) => Task.FromResult(true))
            .WhenTrue("yes").WhenFalse("no").Create("operand");

        // Act
        var policy = (AsyncNotPolicy<int, string>)operand.Not();
        var unary = (IAsyncUnaryOperationSpec)policy;
        var matches = await policy.MatchesAsync(1);

        // Assert
        policy.Underlying.ShouldBe([operand]);
        unary.Operand.ShouldBeSameAs(operand);
        unary.Operation.ShouldBe(Operator.Not);
        unary.IsCollapsable.ShouldBeFalse();
        matches.ShouldBeFalse();
    }

    #endregion

    #region AsyncNotSpecDescription.cs — negation statement/detail branches

    [Fact]
    public void Should_strip_a_leading_bang_and_detect_a_nested_AsyncNotSpec_operand()
    {
        // Arrange — explicitly typed as AsyncSpecBase (not AsyncPolicyBase) so Not() resolves to the
        // Spec-producing overload: spec.Not() ("!x") .Not() (strips to "x", still typed AsyncNotSpec)
        // .Not() (re-adds "!")
        AsyncSpecBase<int, string> spec = Spec.BuildAsync((int _) => Task.FromResult(true)).Create("x");

        // Act
        var tripleNegated = spec.Not().Not().Not();
        var statement = tripleNegated.Description.Statement;
        var detailed = tripleNegated.Description.Detailed;
        var reason = tripleNegated.Description.ToReason(true);
        var descriptionToString = tripleNegated.Description.ToString();

        // Assert
        statement.ShouldBe("!x");
        detailed.ShouldNotBeNull();
        reason.ShouldNotBeNull();
        descriptionToString!.ShouldBe(statement);
        tripleNegated.ToString().ShouldBe(statement);
    }

    [Fact]
    public void Should_strip_a_leading_bang_and_detect_a_nested_AsyncNotPolicy_operand()
    {
        // Arrange
        AsyncPolicyBase<int, string> policy = Spec.BuildAsync((int _) => Task.FromResult(true)).Create("x");

        // Act
        var tripleNegated = policy.Not().Not().Not();
        var statement = tripleNegated.Description.Statement;

        // Assert
        statement.ShouldBe("!x");
    }

    [Fact]
    public void Should_detect_a_nested_sync_NotSpec_operand_reached_through_an_adapter()
    {
        // Arrange — a sync double-negation ("x".Not().Not()) keeps the NotSpec type but strips the leading "!",
        // then a single async negation of the adapter-lifted result must recurse into the adapter's Underlying
        // to find the sync NotSpec. Explicitly typed as SpecBase (not PolicyBase) so Not() resolves to the
        // Spec-producing overload.
        SpecBase<int, string> sync = Spec.Build((int _) => true).Create("x");
        var syncDoubleNegated = sync.Not().Not();

        // Act
        var negated = syncDoubleNegated.ToAsyncSpec().Not();
        var statement = negated.Description.Statement;

        // Assert
        statement.ShouldBe("!x");
    }

    [Fact]
    public void Should_detect_a_nested_sync_NotPolicy_operand_reached_through_an_adapter()
    {
        // Arrange
        PolicyBase<int, string> sync = Spec.Build((int _) => true).Create("x");
        var syncDoubleNegated = sync.Not().Not();

        // Act
        var negated = syncDoubleNegated.ToAsyncSpec().Not();
        var statement = negated.Description.Statement;

        // Assert
        statement.ShouldBe("!x");
    }

    [Fact]
    public void Should_detect_a_lifted_sync_binary_composite_when_formatting_the_negated_statement()
    {
        // Arrange — a sync AND composite (a binary-operation marker) lifted as the sole operand of an async Not
        var a = Spec.Build((int _) => true).Create("a");
        var b = Spec.Build((int _) => true).Create("b");
        var composite = b.And(a.Not());

        // Act
        var negated = composite.ToAsyncSpec().Not();
        var statement = negated.Description.Statement;

        // Assert — since the composite contains a binary operation, the whole statement is parenthesized
        statement.ShouldBe($"!({composite.Description.Statement})");
    }

    #endregion

    #region AsyncBinarySpecDescription.cs — ToReason / ToString

    [Fact]
    public void Should_render_ToReason_and_ToString_on_an_async_binary_description()
    {
        // Arrange
        var left = Spec.BuildAsync((int _) => Task.FromResult(true)).Create("left");
        var right = Spec.BuildAsync((int _) => Task.FromResult(true)).Create("right");
        var composed = left.And(right);

        // Act
        var toReason = composed.Description.ToReason(true);
        var descriptionToString = composed.Description.ToString();
        var toString = composed.ToString();

        // Assert
        toReason.ShouldNotBeNullOrWhiteSpace();
        descriptionToString!.ShouldBe(composed.Description.Statement);
        toString.ShouldBe(composed.Description.Statement);
    }

    #endregion

    #region SyncToAsyncAdapter — SyncSpecAsyncAdapter.cs

    [Fact]
    public async Task Should_expose_Underlying_and_succeed_MatchesAsync_on_SyncSpecAsyncAdapter()
    {
        // Arrange
        var sync = Spec.Build((int _) => true).WhenTrue("yes").WhenFalse("no").Create("sync");

        // Act
        var adapter = sync.ToAsyncSpec();
        var matches = await adapter.MatchesAsync(1);

        // Assert
        adapter.Underlying.ShouldBe([sync]);
        matches.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_fault_MatchesAsync_and_EvaluateAsync_when_the_lifted_sync_spec_throws()
    {
        // Arrange — WhenTrueYield forces a Spec (not a Policy) so the SyncSpecAsyncAdapter path is exercised
        var throwing = Spec.Build((int _) => Throw())
            .WhenTrueYield(_ => ["a"])
            .WhenFalseYield(_ => ["b"])
            .Create("throws");
        var adapter = throwing.ToAsyncSpec();

        // Act
        var matchesTask = adapter.MatchesAsync(1);
        var evaluateTask = adapter.EvaluateAsync(1);

        // Assert
        await matchesTask.ShouldThrowAsync<InvalidOperationException>();
        await evaluateTask.ShouldThrowAsync<InvalidOperationException>();
        return;

        static bool Throw() => throw new InvalidOperationException("boom");
    }

    [Fact]
    public void Should_return_itself_from_ToAsyncExplanationSpec_when_already_string_metadata()
    {
        // Arrange — WhenTrueYield forces a genuine Spec (not a Policy) so ToAsyncSpec() produces a
        // SyncSpecAsyncAdapter rather than a SyncPolicyAsyncAdapter
        var sync = Spec.Build((int _) => true)
            .WhenTrueYield(_ => ["yes"])
            .WhenFalseYield(_ => ["no"])
            .Create("sync");

        // Act
        var adapter = sync.ToAsyncSpec();
        var explanationSpec = adapter.ToAsyncExplanationSpec();

        // Assert
        explanationSpec.ShouldBeSameAs(adapter);
    }

    [Fact]
    public async Task Should_lower_to_an_explanation_spec_from_ToAsyncExplanationSpec_when_non_string_metadata()
    {
        // Arrange — WhenTrueYield forces a genuine Spec (not a Policy) so ToAsyncSpec() produces a
        // SyncSpecAsyncAdapter rather than a SyncPolicyAsyncAdapter
        var sync = Spec.Build((int _) => true)
            .WhenTrueYield(_ => new[] { 'P' })
            .WhenFalseYield(_ => new[] { 'F' })
            .Create("sync");

        // Act
        var adapter = sync.ToAsyncSpec();
        var explanationSpec = adapter.ToAsyncExplanationSpec();
        var result = await explanationSpec.EvaluateAsync(1);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["sync == true"]);
    }

    #endregion

    #region SyncToAsyncAdapter — SyncPolicyAsyncAdapter.cs

    [Fact]
    public async Task Should_expose_Underlying_and_fault_MatchesAsync_on_SyncPolicyAsyncAdapter()
    {
        // Arrange — a minimal proposition guarantees Policy semantics
        var throwingPolicy = Spec.Build((int _) => Throw()).Create("throws");
        var adapter = throwingPolicy.ToAsyncSpec();

        // Act
        var matchesTask = adapter.MatchesAsync(1);

        // Assert
        adapter.Underlying.ShouldBe([throwingPolicy]);
        await matchesTask.ShouldThrowAsync<InvalidOperationException>();
        return;

        static bool Throw() => throw new InvalidOperationException("boom");
    }

    [Fact]
    public void Should_return_itself_from_ToAsyncExplanationSpec_on_a_string_metadata_policy_adapter()
    {
        // Arrange
        var policy = Spec.Build((int _) => true).WhenTrue("yes").WhenFalse("no").Create("policy");

        // Act
        var adapter = policy.ToAsyncSpec();
        var explanationSpec = adapter.ToAsyncExplanationSpec();

        // Assert
        explanationSpec.ShouldBeSameAs(adapter);
    }

    [Fact]
    public async Task Should_lower_to_an_explanation_spec_on_a_non_string_metadata_policy_adapter()
    {
        // Arrange
        var policy = Spec.Build((int _) => true).WhenTrue('P').WhenFalse('F').Create("policy");

        // Act
        var adapter = policy.ToAsyncSpec();
        var explanationSpec = adapter.ToAsyncExplanationSpec();
        var result = await explanationSpec.EvaluateAsync(1);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["policy == true"]);
    }

    #endregion

    #region MetadataToExplanationAdapter — AsyncMetadataToExplanationAdapterSpec.cs

    [Fact]
    public async Task Should_forward_Underlying_Description_and_MatchesAsync_through_the_metadata_adapter()
    {
        // Arrange — a genuine async metadata proposition (non-string) forces the adapter into existence
        AsyncSpecBase<int, char> metadataSpec = Spec.BuildAsync((int _) => Task.FromResult(true))
            .WhenTrue('P').WhenFalse('F').Create("grade");

        // Act — go through the untyped base reference so MatchesAsync forwards via the adapter
        AsyncSpecBase<int> untyped = metadataSpec.ToAsyncExplanationSpec();
        var matches = await untyped.MatchesAsync(1);

        // Assert
        untyped.Underlying.ShouldBe([metadataSpec]);
        untyped.Description.ShouldBeSameAs(metadataSpec.Description);
        matches.ShouldBeTrue();
    }

    #endregion

    #region BooleanPredicateProposition — direct MatchesAsync + Underlying on leaf propositions

    [Fact]
    public async Task Should_directly_invoke_MatchesAsync_on_an_unnamed_async_explanation_proposition()
    {
        // Arrange
        var spec = Spec.BuildAsync((bool b) => Task.FromResult(b))
            .WhenTrue("user is active")
            .WhenFalse("user is not active")
            .Create();

        // Act
        var matches = await spec.MatchesAsync(true);

        // Assert
        matches.ShouldBeTrue();
        spec.Underlying.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_directly_invoke_MatchesAsync_on_a_named_async_metadata_proposition()
    {
        // Arrange
        var spec = Spec.BuildAsync((bool b) => Task.FromResult(b))
            .WhenTrue('P').WhenFalse('F')
            .Create("grade");

        // Act
        var matches = await spec.MatchesAsync(true);

        // Assert
        matches.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_directly_invoke_MatchesAsync_on_an_unnamed_multi_assertion_explanation_proposition()
    {
        // Arrange
        var spec = Spec.BuildAsync((bool b) => Task.FromResult(b))
            .WhenTrue("all good")
            .WhenFalseYield(_ => ["bad one", "bad two"])
            .Create();

        // Act
        var matches = await spec.MatchesAsync(true);

        // Assert
        matches.ShouldBeTrue();
        spec.Underlying.ShouldBeEmpty();
        spec.Description.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_directly_invoke_MatchesAsync_on_a_named_multi_metadata_proposition()
    {
        // Arrange
        var spec = Spec.BuildAsync((bool b) => Task.FromResult(b))
            .WhenTrueYield(_ => new[] { 1, 2 })
            .WhenFalseYield(_ => new[] { 3 })
            .Create("numbers");

        // Act
        var matches = await spec.MatchesAsync(true);

        // Assert
        matches.ShouldBeTrue();
        spec.Underlying.ShouldBeEmpty();
        spec.Description.ShouldNotBeNull();
    }

    #endregion

    #region PropositionBuilders — the untested named/singular factory Create() overloads

    [Fact]
    public async Task Should_support_the_named_Create_overload_on_the_multi_assertion_explanation_factory()
    {
        // Arrange — WhenTrue(string).WhenFalseYield(...).Create("name") is the factory's untested named path
        var spec = Spec.BuildAsync((bool b) => Task.FromResult(b))
            .WhenTrue("all good")
            .WhenFalseYield(_ => ["bad one", "bad two"])
            .Create("all good check");

        // Act
        var trueResult = await spec.EvaluateAsync(true);
        var falseResult = await spec.EvaluateAsync(false);

        // Assert — a supplied name demotes the because-strings to Values
        trueResult.Assertions.ShouldBe(["all good check == true"]);
        trueResult.Values.ShouldBe(["all good"]);
        falseResult.Assertions.ShouldBe(["all good check == false"]);
        falseResult.Values.ShouldBe(["bad one", "bad two"]);
    }

    [Fact]
    public async Task Should_support_the_singular_WhenTrue_with_WhenFalseYield_metadata_factory()
    {
        // Arrange — WhenTrue(value).WhenFalseYield(...).Create("name") is never exercised elsewhere
        var spec = Spec.BuildAsync((bool b) => Task.FromResult(b))
            .WhenTrue(1)
            .WhenFalseYield(_ => new[] { 2, 3 })
            .Create("numbers");

        // Act
        var trueResult = await spec.EvaluateAsync(true);
        var falseResult = await spec.EvaluateAsync(false);

        // Assert
        trueResult.Values.ShouldBe([1]);
        falseResult.Values.ShouldBe([2, 3]);
    }

    #endregion
}
