namespace Motiv.Serialization.Tests;

public class RuleSerializerOptionsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_reject_a_MaxDocumentDepth_below_one(int value)
    {
        // Arrange
        var options = new RuleSerializerOptions();

        // Act
        var act = () => { options.MaxDocumentDepth = value; };

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_reject_a_MaxNodeCount_below_one(int value)
    {
        // Arrange
        var options = new RuleSerializerOptions();

        // Act
        var act = () => { options.MaxNodeCount = value; };

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// The cap is a deliberate cost-per-evaluation budget rather than a round number, so a change to it
    /// should be a change to this test and to the derivation in its XML docs — not a silent edit.
    /// </summary>
    [Fact]
    public void Should_default_MaxCompositionDepth_to_the_re_derived_cap() =>
        new RuleSerializerOptions().MaxCompositionDepth.ShouldBe(4_096);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_reject_a_MaxCompositionDepth_below_one(int value)
    {
        // Arrange
        var options = new RuleSerializerOptions();

        // Act
        var act = () => { options.MaxCompositionDepth = value; };

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// The stack budget, separate from <c>MaxCompositionDepth</c>'s cost budget because decorator
    /// nesting is the one shape Spec 3E left recursive. Derived from #145's measurement: the
    /// alternating operator/decorator shape returns at 261 layers asynchronously on a 1 MB thread, so
    /// 128 leaves rather more than a factor of two. Changing it should change this test and the
    /// derivation in its XML docs.
    /// </summary>
    [Fact]
    public void Should_default_MaxDecoratorDepth_to_half_the_measured_async_ceiling() =>
        new RuleSerializerOptions().MaxDecoratorDepth.ShouldBe(128);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_reject_a_MaxDecoratorDepth_below_one(int value)
    {
        // Arrange
        var options = new RuleSerializerOptions();

        // Act
        var act = () => { options.MaxDecoratorDepth = value; };

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Decorator levels are counted within one document too, not only across references: every node
    /// carrying a <c>name</c> is a wrapper the binder composes.
    /// </summary>
    [Fact]
    public void Should_refuse_a_document_nesting_more_decorators_than_the_decorator_cap()
    {
        // Arrange
        var registry = new SpecRegistry()
            .Register("a", Spec.Build((int n) => n > 0).WhenTrue("t").WhenFalse("f").Create());
        var serializer = new RuleSerializer(registry, new RuleSerializerOptions { MaxDecoratorDepth = 2 });

        // Four nested named nodes.
        const string json =
            """
            { "name": "d1",
              "rule": { "name": "d2", "not": {
                          "name": "d3", "not": { "name": "d4", "spec": "a" } } } }
            """;

        // Act
        var errors = serializer.Validate<int>(json);

        // Assert
        errors.ShouldContain(error => error.Code == RuleErrorCode.DocumentTooLarge);
    }

    [Fact]
    public void Should_validate_without_overflowing_when_MaxDocumentDepth_is_int_MaxValue()
    {
        // Arrange
        var options = new RuleSerializerOptions { MaxDocumentDepth = int.MaxValue };
        var serializer = new RuleSerializer(new SpecRegistry(), options);

        // Act
        var errors = serializer.Validate("""{ "rule": { "spec": "a" } }""");

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Should_reject_a_null_registry()
    {
        // Act
        var act = () => new RuleSerializer(null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }
}
