using System.Linq.Expressions;
using Motiv.ExpressionTrees;

namespace Motiv.Tests;

public class ExpressionTreeTests
{
    [Theory]
    [InlineData(1.5, false)]
    [InlineData(1.0, true)]
    public void Should_evaluate_boolean_expression_from_an_expression_tree_spec(double model, bool expectedResult)
    {
        // Arrange
        var sut =
            Spec.From((double n) => Math.Abs(n % 1) < double.Epsilon)
                .Create("is integer");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(1.5, false)]
    [InlineData(-1.0, false)]
    [InlineData(-1.5, false)]
    [InlineData(1.0, true)]
    public void Should_evaluate_boolean_expression_containing_an_and(double model, bool expectedResult)
    {
        // Arrange
        var sut =
            Spec.From((double n) => Math.Abs(n % 1) < double.Epsilon & n >= 0)
                .Create("and expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(1.5, false)]
    [InlineData(-1.0, false)]
    [InlineData(-1.5, false)]
    [InlineData(1.0, true)]
    public void Should_evaluate_boolean_expression_containing_a_conditional_and(double model, bool expectedResult)
    {
        // Arrange
        var sut =
            Spec.From((double n) => Math.Abs(n % 1) < double.Epsilon && n >= 0)
                .Create("conditional and expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(1.5, true)]
    [InlineData(-1.0, true)]
    [InlineData(-1.5, false)]
    [InlineData(1.0, true)]
    public void Should_evaluate_boolean_expression_containing_an_or(double model, bool expectedResult)
    {
        // Arrange
        var sut =
            Spec.From((double n) => Math.Abs(n % 1) < double.Epsilon | n >= 0)
                .Create("or expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(1.5, true)]
    [InlineData(-1.0, true)]
    [InlineData(-1.5, false)]
    [InlineData(1.0, true)]
    public void Should_evaluate_boolean_expression_containing_a_conditional_or(double model, bool expectedResult)
    {
        // Arrange
        var sut =
            Spec.From((double n) => Math.Abs(n % 1) < double.Epsilon || n >= 0)
                .Create(" or expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(1.5, true)]
    [InlineData(-1.0, true)]
    [InlineData(-1.5, false)]
    [InlineData(1.0, false)]
    public void Should_evaluate_boolean_expression_containing_exclusive_or(double model, bool expectedResult)
    {
        // Arrange
        var sut =
            Spec.From((double n) => Math.Abs(n % 1) < double.Epsilon ^ n >= 0)
                .Create("xor expression");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(3, "checked(n + 1) > n")]
    public void Should_assert_expressions_containing_checked_operations(int model, string expectedAssertion)
    {
        // Arrange
        var sut = Spec
            .From((int n) => checked(n + 1) > n)
            .Create("checked-operation");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Fact]
    public void Should_assert_expressions_containing_checked_operations_and_throw_when_overflowing()
    {
        // Arrange
        var sut = Spec
            .From((int n) => checked(n + 1) > n)
            .Create("checked-operation");

        // Act
        var act = () => sut.IsSatisfiedBy(int.MaxValue);

        act.Should().Throw<OverflowException>();
    }

    [Theory]
    [InlineData(5, "Enumerable.Range(1, n).Sum() == 15")]
    [InlineData(3, "Enumerable.Range(1, n).Sum() == 6")]
    public void Should_evaluate_display_as_value_call_on_complex_expressions(int model, string expectedAssertion)
    {
        // Arrange
        var sut = Spec
            .From((int n) => Enumerable.Range(1, n).Sum() == Display.AsValue(n * (n + 1) / 2))
            .Create("linq-method-call");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, "arr.Length == 3")]
    [InlineData(new[] { 1 }, "arr.Length != 3")]
    public void Should_assert_expressions_containing_array_length_property(int[] model, string expectedAssertion)
    {
        // Arrange
        var sut = Spec
            .From((int[] arr) => arr.Length == 3)
            .Create("array-length");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    [Theory]
    [InlineData("test", true, "str.ToCharArray().Any((char c) => char.IsUpper(c)) == false")]
    [InlineData("Test", false, "str.ToCharArray().Any((char c) => char.IsUpper(c)) == true")]
    public void Should_assert_expressions_containing_nested_lambda_expressions(string model, bool expectedResult, string expectedAssertion)
    {
        // Arrange
        var sut = Spec
            .From((string str) => !str.ToCharArray().Any(c => char.IsUpper(c)))
            .Create("nested-lambda");

        // Act
        var act = sut.IsSatisfiedBy(model);

        // Assert
        act.Satisfied.Should().Be(expectedResult);
        act.Assertions.Should().BeEquivalentTo(expectedAssertion);
    }

    public class SoftwareBuild
    {
        public string BuildId => Guid.NewGuid().ToString();
        public bool CompilationSuccessful => true;
        public double TestCoverage => 0.75;
        public IDictionary<string, bool> TestResults => new Dictionary<string, bool>
        {
            { "UnitTests", true },
            { "IntegrationTests", true },
            { "EndToEndTests", true }
        };
        public IDictionary<string, double> PerformanceMetrics => new Dictionary<string, double>
        {
            { "ResponseTime", 150 },
            { "CPUUsage", 50 },
            { "MemoryUsage", 70 }
        };
        public IList<string> SecurityVulnerabilities => new List<string>
        {
            "Critical: CVE-2021-1234",
            "High: CVE-2021-5678",
            "Medium: CVE-2021-9012"
        };
        public IDictionary<string, Version> Dependencies => new Dictionary<string, Version>
        {
            { "ExternalDependency1", new Version("1.0") },
            { "ExternalDependency2", new Version("1.5") },
            { "InternalDependency1", new Version("2.0") },
            { "InternalDependency2", new Version("2.5") }
        };
        public IDictionary<string, bool> FeatureFlags => new Dictionary<string, bool>
        {
            { "Monitoring", true },
            { "Logging", true },
            { "Caching", true },
            { "LegacyFeature", false }
        };
        public IDictionary<string, bool> EnvironmentChecks => new Dictionary<string, bool>
        {
            { "Database", true },
            { "Cache", true },
            { "MessageQueue", true },
            { "CDN", true }
        };
        public ISet<string> Approvals => new HashSet<string> { "QA", "Security", "ProductOwner" };
        public Dictionary<string, bool> ComplianceChecks => new Dictionary<string, bool>
        {
            { "VulnerabilityException", true },
            { "ComplianceCheck1", true },
            { "ComplianceCheck2", true },
            { "ComplianceCheck3", true }
        };

        public string ReleaseNotes =>
            """
            This release includes several bug fixes and performance improvements.
            """;
        public DateTime BuildTimestamp => DateTime.Now.AddDays(-7);
    }

    [Fact]
    public void Should_evaluate_complex_expression()
    {
        Expression<Func<SoftwareBuild, bool>> isSoftwareBuildReadyForProductionDeployment = build =>
            build.CompilationSuccessful &
            build.TestCoverage >= 0.85 &
            build.TestResults.Count(r => r.Value) == build.TestResults.Count &
            build.PerformanceMetrics.All(m => m.Value <=
                                              (m.Key == "ResponseTime" ? 200 : m.Key == "CPUUsage" ? 70 : 500)) &
            !build.SecurityVulnerabilities.Any(v => v.StartsWith("Critical:") || v.StartsWith("High:")) &
            build.Dependencies.All(d => d.Value >= new Version(d.Key.Contains("Legacy") ? "1.5" : "2.0")) &
            build.FeatureFlags.Count(f => f.Value) >= 3 &
            build.EnvironmentChecks.All(e => e.Value) &
            build.Approvals.IsSupersetOf(new[] { "QA", "Security", "ProductOwner" }) &
            build.ComplianceChecks.All(c => c.Value) &
            !string.IsNullOrWhiteSpace(build.ReleaseNotes) &
            build.ReleaseNotes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length >= 5 &
            (DateTime.Now - build.BuildTimestamp).TotalDays <= 14 &
            build.BuildId.StartsWith(DateTime.Now.ToString("yyyy.MM")) &
            (build.TestResults.Count >= 100 ?
                build.TestCoverage > 0.90 :
                build.Approvals.Contains("CTO")) &
            (build.PerformanceMetrics.ContainsKey("MemoryUsage") ?
                build.PerformanceMetrics["MemoryUsage"] < 80 :
                build.PerformanceMetrics.Count < 5) &
            build.Dependencies.Count(d => d.Key.StartsWith("External")) <
            build.Dependencies.Count(d => d.Key.StartsWith("Internal")) &
            (build.SecurityVulnerabilities.Count == 0 ||
                (build.Approvals.Contains("CISO") && build.ComplianceChecks["VulnerabilityException"])) &
            build.FeatureFlags.Any(f => f.Key.Contains("Monitoring") && f.Value);
        // Arrange
        var sut = Spec
            .From(isSoftwareBuildReadyForProductionDeployment)
            .Create("complex-expression");

        // Act
        var act = sut.IsSatisfiedBy(new SoftwareBuild());

        // Assert
        act.Satisfied.Should().BeFalse();
        act.Assertions.Should().BeEquivalentTo(
            "build.Approvals.Contains(\"CISO\") == false",
            "build.Approvals.Contains(\"CTO\") == false",
            "build.BuildId.StartsWith(DateTime.Now.ToString(\"yyyy.MM\")) == false",
            "build.Dependencies.All((KeyValuePair<string, Version> d) => d.Value >= new Version(d.Key.Contains(\"Legacy\") ? \"1.5\" : \"2.0\")) == false",
            "build.Dependencies.Count((KeyValuePair<string, Version> d) => d.Key.StartsWith(\"External\")) >= build.Dependencies.Count((KeyValuePair<string, Version> d) => d.Key.StartsWith(\"Internal\"))",
            "build.ReleaseNotes.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length < 5",
            "build.SecurityVulnerabilities.Any((string v) => v.StartsWith(\"Critical:\") || v.StartsWith(\"High:\")) == true",
            "build.SecurityVulnerabilities.Count != 0",
            "build.TestCoverage < 0.85",
            "build.TestResults.Count < 100"
        );
    }
}
