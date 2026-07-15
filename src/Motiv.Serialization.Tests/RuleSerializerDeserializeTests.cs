using Motiv.Serialization;
using static Motiv.Serialization.Tests.SpecAssertions;

namespace Motiv.Serialization.Tests;

public class RuleSerializerDeserializeTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue("is positive")
            .WhenFalse("is not positive")
            .Create();

    private static SpecBase<int, int> HasFlag { get; } =
        Spec.Build((int n) => n != 0)
            .WhenTrue(1)
            .WhenFalse(0)
            .Create("has flag");

    private static RuleSerializer CreateSerializer() =>
        new(new SpecRegistry()
            .Register("is-positive", IsPositive)
            .Register("has-flag", HasFlag));

    [Fact]
    public void Should_load_a_bare_registry_leaf_that_behaves_identically_to_the_fluent_spec()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "is-positive" } }""";

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, IsPositive, 5, -5);
    }

    [Fact]
    public void Should_load_a_decorated_leaf_like_a_fluent_explanation_wrapper()
    {
        // Arrange
        const string json =
            """{ "rule": { "spec": "is-positive", "whenTrue": "ok", "whenFalse": "bad" } }""";
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue("ok")
            .WhenFalse("bad")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_load_a_named_decorated_leaf_like_a_named_fluent_wrapper()
    {
        // Arrange
        const string json =
            """{ "rule": { "spec": "is-positive", "whenTrue": "ok", "whenFalse": "bad", "name": "check" } }""";
        var expected = Spec
            .Build(IsPositive)
            .WhenTrue("ok")
            .WhenFalse("bad")
            .Create("check");

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_load_a_name_only_node_like_a_fluent_create_wrapper()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "is-positive", "name": "wrapper" } }""";
        var expected = Spec
            .Build(IsPositive)
            .Create("wrapper");

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_wrap_the_root_with_the_document_name()
    {
        // Arrange
        const string json = """{ "name": "document rule", "rule": { "spec": "is-positive" } }""";
        var expected = Spec
            .Build(IsPositive)
            .Create("document rule");

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, expected, 5, -5);
    }

    [Fact]
    public void Should_degrade_a_non_string_metadata_leaf_to_its_explanation_spec()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "has-flag" } }""";

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, HasFlag.ToExplanationSpec(), 3, 0);
    }

    [Fact]
    public void Should_decorate_a_non_string_metadata_leaf_with_explanation_strings()
    {
        // Arrange
        const string json =
            """{ "rule": { "spec": "has-flag", "whenTrue": "on", "whenFalse": "off" } }""";
        var expected = Spec
            .Build(HasFlag)
            .WhenTrue("on")
            .WhenFalse("off")
            .Create();

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        // Justification is intentionally not compared here: the loader decorates the entry's
        // explanation view rather than the original generic spec, which may differ in the
        // underlying layers while agreeing on the outcome, reason, and assertions.
        foreach (var model in new[] { 3, 0 })
        {
            var expectedResult = expected.Evaluate(model);
            var actualResult = loaded.Evaluate(model);
            actualResult.Satisfied.ShouldBe(expectedResult.Satisfied);
            actualResult.Reason.ShouldBe(expectedResult.Reason);
            actualResult.Assertions.ShouldBe(expectedResult.Assertions);
        }
    }

    [Fact]
    public void Should_bind_a_not_node_to_the_fluent_negation()
    {
        // Arrange
        const string json = """{ "rule": { "not": { "spec": "is-positive" } } }""";

        // Act
        var loaded = CreateSerializer().Deserialize<int>(json);

        // Assert
        ShouldBehaveIdentically(loaded, IsPositive.Not(), 5, -5);
    }

    [Fact]
    public void Should_throw_for_an_unknown_spec_name()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "missing" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.UnknownSpec);
        error.Path.ShouldBe("$.rule");
        error.Message.ShouldContain("missing");
    }

    [Fact]
    public void Should_throw_for_a_model_type_mismatch()
    {
        // Arrange
        const string json = """{ "rule": { "spec": "is-positive" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<string>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ModelTypeMismatch);
        error.Message.ShouldContain("Int32");
        error.Message.ShouldContain("String");
    }

    [Fact]
    public void Should_throw_when_a_sync_load_references_an_async_spec()
    {
        // Arrange
        var isReadyAsync = Spec
            .BuildAsync((int n) => Task.FromResult(n > 0))
            .Create("is ready");
        var registry = new SpecRegistry().Register("is-ready", isReadyAsync);
        var serializer = new RuleSerializer(registry);
        const string json = """{ "rule": { "spec": "is-ready" } }""";

        // Act
        var act = () => serializer.Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.AsyncSpecInSyncLoad);
        error.Message.ShouldContain("is-ready");
    }

    [Fact]
    public void Should_throw_for_an_expression_node_when_expressions_are_not_enabled()
    {
        // Arrange
        const string json = """{ "rule": { "expression": "Age >= 18" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ExpressionsNotEnabled);
        error.Message.ShouldContain("Motiv.Serialization.Expressions");
    }

    [Fact]
    public void Should_throw_for_object_payloads_in_an_explanation_load()
    {
        // Arrange
        const string json =
            """{ "rule": { "spec": "is-positive", "whenTrue": { "code": 1 }, "whenFalse": { "code": 2 } } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
        error.Path.ShouldBe("$.rule");
    }

    [Fact]
    public void Should_throw_structural_errors_from_deserialize()
    {
        // Arrange
        const string json = """{ "rule": { "name": "no operator" } }""";

        // Act
        var act = () => CreateSerializer().Deserialize<int>(json);

        // Assert
        var exception = act.ShouldThrow<RuleSerializationException>();
        exception.Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.InvalidNode);
    }
}
