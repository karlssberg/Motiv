namespace Motiv.Tests;

public class PolicyExtensionsTests
{
    [Theory]
    [InlineData(true, false, true, "first")]
    [InlineData(false, true, true, "second")]
    [InlineData(true, true, true, "first")]
    [InlineData(false, false, false, "third")]
    public void Should_evaluate_policies_using_OrElseTogether_with_string_metadata(
        bool firstSatisfied,
        bool secondSatisfied,
        bool expectedSatisfied,
        string expectedMetadata)
    {
        // Arrange
        var firstPolicy = Spec
            .Build<string>(_ => firstSatisfied)
            .WhenTrue("first")
            .WhenFalse("first-false")
            .Create("first policy");

        var secondPolicy = Spec
            .Build<string>(_ => secondSatisfied)
            .WhenTrue("second")
            .WhenFalse("second-false")
            .Create("second policy");

        var thirdPolicy = Spec
            .Build<string>(_ => false)
            .WhenTrue("third")
            .WhenFalse("third")
            .Create("third policy");

        var policies = new[] { firstPolicy, secondPolicy, thirdPolicy };
        var model = "test";

        // Act
        var combinedPolicy = policies.OrElseTogether();
        var result = combinedPolicy.Evaluate(model);

        // Assert
        result.Satisfied.ShouldBe(expectedSatisfied);
        result.Value.ShouldBe(expectedMetadata);
    }

    [Theory]
    [InlineData(5, 10, 15, true, 5)]
    [InlineData(20, 10, 15, true, 10)]
    [InlineData(20, 25, 15, false, 15)]
    [InlineData(20, 25, 30, false, 30)]
    public void Should_evaluate_policies_using_OrElseTogether_with_numeric_metadata(
        int firstThreshold,
        int secondThreshold,
        int thirdThreshold,
        bool expectedSatisfied,
        int expectedValue)
    {
        // Arrange
        var model = 12;

        var firstPolicy = Spec
            .Build<int>(n => n > firstThreshold)
            .WhenTrue(firstThreshold)
            .WhenFalse(firstThreshold)
            .Create("first threshold");

        var secondPolicy = Spec
            .Build<int>(n => n > secondThreshold)
            .WhenTrue(secondThreshold)
            .WhenFalse(secondThreshold)
            .Create("second threshold");

        var thirdPolicy = Spec
            .Build<int>(n => n > thirdThreshold)
            .WhenTrue(thirdThreshold)
            .WhenFalse(thirdThreshold)
            .Create("third threshold");

        var policies = new[] { firstPolicy, secondPolicy, thirdPolicy };

        // Act
        var combinedPolicy = policies.OrElseTogether();
        var result = combinedPolicy.Evaluate(model);

        // Assert
        result.Satisfied.ShouldBe(expectedSatisfied);
        result.Value.ShouldBe(expectedValue);
    }

    [Fact]
    public void Should_handle_single_policy_in_OrElseTogether()
    {
        // Arrange
        var singlePolicy = Spec
            .Build<string>(_ => true)
            .WhenTrue("success")
            .WhenFalse("failure")
            .Create("single policy");

        var policies = new[] { singlePolicy };
        var model = "test";

        // Act
        var combinedPolicy = policies.OrElseTogether();
        var result = combinedPolicy.Evaluate(model);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Value.ShouldBe("success");
    }

    [Fact]
    public void Should_throw_exception_for_empty_collection_in_OrElseTogether()
    {
        // Arrange
        var emptyPolicies = Array.Empty<PolicyBase<string, string>>();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => emptyPolicies.OrElseTogether());
        exception.Message.ShouldContain("no elements");
    }

    [Fact]
    public void Should_stop_at_first_satisfied_policy_in_OrElseTogether()
    {
        // Arrange
        var evaluationCount = 0;

        var firstPolicy = Spec
            .Build<string>(_ =>
            {
                evaluationCount++;
                return false;
            })
            .WhenTrue("first-true")
            .WhenFalse("first-false")
            .Create("first policy");

        var secondPolicy = Spec
            .Build<string>(_ =>
            {
                evaluationCount++;
                return true;
            })
            .WhenTrue("second-true")
            .WhenFalse("second-false")
            .Create("second policy");

        var thirdPolicy = Spec
            .Build<string>(_ =>
            {
                evaluationCount++;
                return true;
            })
            .WhenTrue("third-true")
            .WhenFalse("third-false")
            .Create("third policy");

        var policies = new[] { firstPolicy, secondPolicy, thirdPolicy };
        var model = "test";

        // Act
        var combinedPolicy = policies.OrElseTogether();
        var result = combinedPolicy.Evaluate(model);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Value.ShouldBe("second-true");
        evaluationCount.ShouldBe(2); // Should stop after second policy is satisfied
    }

    [Theory]
    [AutoData]
    public void Should_return_last_policy_false_metadata_when_all_policies_fail(string model)
    {
        // Arrange
        var firstPolicy = Spec
            .Build<string>(_ => false)
            .WhenTrue("first-true")
            .WhenFalse("first-false")
            .Create("first policy");

        var secondPolicy = Spec
            .Build<string>(_ => false)
            .WhenTrue("second-true")
            .WhenFalse("second-false")
            .Create("second policy");

        var thirdPolicy = Spec
            .Build<string>(_ => false)
            .WhenTrue("third-true")
            .WhenFalse("last-false")
            .Create("third policy");

        var policies = new[] { firstPolicy, secondPolicy, thirdPolicy };

        // Act
        var combinedPolicy = policies.OrElseTogether();
        var result = combinedPolicy.Evaluate(model);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("last-false");
    }

    [Fact]
    public void Should_work_with_complex_metadata_types()
    {
        // Arrange
        var complexMetadata1 = new { Id = 1, Name = "First" };
        var complexMetadata2 = new { Id = 2, Name = "Second" };
        var complexMetadata3 = new { Id = 3, Name = "Third" };

        var firstPolicy = Spec
            .Build<int>(n => n < 5)
            .WhenTrue(complexMetadata1)
            .WhenFalse(complexMetadata1)
            .Create("first policy");

        var secondPolicy = Spec
            .Build<int>(n => n > 10)
            .WhenTrue(complexMetadata2)
            .WhenFalse(complexMetadata2)
            .Create("second policy");

        var thirdPolicy = Spec
            .Build<int>(n => n > 0)
            .WhenTrue(complexMetadata3)
            .WhenFalse(complexMetadata3)
            .Create("third policy");

        var policies = new[] { firstPolicy, secondPolicy, thirdPolicy };
        var model = 7;

        // Act
        var combinedPolicy = policies.OrElseTogether();
        var result = combinedPolicy.Evaluate(model);

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Value.ShouldBe(complexMetadata3);
        result.Value.Id.ShouldBe(3);
        result.Value.Name.ShouldBe("Third");
    }

    [Fact]
    public void Should_preserve_policy_descriptions_in_combined_policy()
    {
        // Arrange
        var firstPolicy = Spec
            .Build<string>(_ => false)
            .WhenTrue("first")
            .WhenFalse("first")
            .Create("is first condition");

        var secondPolicy = Spec
            .Build<string>(_ => false)
            .WhenTrue("second")
            .WhenFalse("second")
            .Create("is second condition");

        var policies = new[] { firstPolicy, secondPolicy };

        // Act
        var combinedPolicy = policies.OrElseTogether();
        var description = combinedPolicy.ToString();

        // Assert
        description.ShouldContain("is first condition");
        description.ShouldContain("is second condition");
        description.ShouldContain("||");
    }

    [Fact]
    public void Should_throw_ArgumentNullException_for_null_policies_collection()
    {
        // Arrange
        IEnumerable<PolicyBase<string, string>> nullPolicies = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => nullPolicies.OrElseTogether());
    }

    [Fact]
    public void Should_flatten_every_cause_of_an_unsatisfied_chain()
    {
        // Arrange
        var policies = new[]
        {
            Spec.Build<string>(_ => false).WhenTrue("a-true").WhenFalse("a-false").Create("a"),
            Spec.Build<string>(_ => false).WhenTrue("b-true").WhenFalse("b-false").Create("b"),
            Spec.Build<string>(_ => false).WhenTrue("c-true").WhenFalse("c-false").Create("c")
        };

        // Act
        var result = policies.OrElseTogether().Evaluate("model");

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("c-false");

        // The chain is a left-nested tree of OrElsePolicyResult, but Values recurses:
        // three policies yield three values, not the root node's two.
        result.Values.ShouldBe(["a-false", "b-false", "c-false"]);

        // Assertions flatten alongside Values...
        result.Assertions.ShouldBe(["a == false", "b == false", "c == false"]);

        // ...but Causes and Underlying describe the binary composition shape: a three-policy
        // chain is a left-nested tree, so its root reports two operands, not three.
        result.Causes.Count().ShouldBe(2);
        result.Underlying.Count().ShouldBe(2);
    }

    [Fact]
    public void Should_report_only_the_causal_value_when_a_chain_is_satisfied()
    {
        // Arrange
        var policies = new[]
        {
            Spec.Build<string>(_ => false).WhenTrue("x-true").WhenFalse("x-false").Create("x"),
            Spec.Build<string>(_ => true).WhenTrue("y-true").WhenFalse("y-false").Create("y"),
            Spec.Build<string>(_ => false).WhenTrue("z-true").WhenFalse("z-false").Create("z")
        };

        // Act
        var result = policies.OrElseTogether().Evaluate("model");

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Value.ShouldBe("y-true");

        // Only causal values appear: neither x nor z is among the causal values.
        result.Values.ShouldBe(["y-true"]);
    }

    [Fact]
    public void Should_retain_every_cause_for_non_string_metadata()
    {
        // Arrange
        var policies = new[]
        {
            Spec.Build<string>(_ => false).WhenTrue(1).WhenFalse(10).Create("m1"),
            Spec.Build<string>(_ => false).WhenTrue(2).WhenFalse(20).Create("m2"),
            Spec.Build<string>(_ => false).WhenTrue(3).WhenFalse(30).Create("m3")
        };

        // Act
        var result = policies.OrElseTogether().Evaluate("model");

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe(30);

        // The guarantee is metadata-agnostic, not string-path-only.
        result.Values.ShouldBe([10, 20, 30]);
    }
}
