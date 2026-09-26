using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix.Tests;

public class ExpressionNameDeriverTests
{
    [Fact]
    public void DeriveClassNames_SingleIdentifier_ReturnsIdentifierBasedNames()
    {
        // age > 18
        var (expression, semanticModel) = CreateExpressionContext("age > 18", "int age");

        var (propositionName, modelName) = ExpressionNameDeriver.DeriveClassNames(
            expression,
            semanticModel,
            0);

        Assert.Equal("AgeProposition", propositionName);
        Assert.Equal("Model", modelName);
    }

    [Fact]
    public void DeriveClassNames_MemberAccessSingleRoot_ReturnsRootBasedNames()
    {
        // order.Total > 100
        var (expression, semanticModel) = CreateExpressionContext(
            "order.Total > 100",
            "Order order",
            """
            public class Order
            {
                public decimal Total { get; set; }
            }
            """);

        var (propositionName, modelName) = ExpressionNameDeriver.DeriveClassNames(
            expression,
            semanticModel,
            0);

        Assert.Equal("OrderProposition", propositionName);
        Assert.Equal("Model", modelName);
    }

    [Fact]
    public void DeriveClassNames_MemberAccessCommonRoot_ReturnsCommonRootNames()
    {
        // order.Total > 100 && order.IsActive
        var (expression, semanticModel) = CreateExpressionContext(
            "order.Total > 100 && order.IsActive",
            "Order order",
            """
            public class Order
            {
                public decimal Total { get; set; }
                public bool IsActive { get; set; }
            }
            """);

        var (propositionName, modelName) = ExpressionNameDeriver.DeriveClassNames(
            expression,
            semanticModel,
            0);

        Assert.Equal("OrderProposition", propositionName);
        Assert.Equal("Model", modelName);
    }

    [Fact]
    public void DeriveClassNames_UnrelatedIdentifiers_ReturnsFallbackNames()
    {
        // x > 5 && y < 10
        var (expression, semanticModel) = CreateExpressionContext("x > 5 && y < 10", "int x, int y");

        var (propositionName, modelName) = ExpressionNameDeriver.DeriveClassNames(
            expression,
            semanticModel,
            0);

        Assert.Equal("Proposition", propositionName);
        Assert.Equal("Model", modelName);
    }

    [Fact]
    public void DeriveClassNames_IsExpression_ReturnVariableBasedNames()
    {
        // obj is string
        var (expression, semanticModel) = CreateExpressionContext("obj is string", "object obj");

        var (propositionName, modelName) = ExpressionNameDeriver.DeriveClassNames(
            expression,
            semanticModel,
            0);

        Assert.Equal("ObjProposition", propositionName);
        Assert.Equal("Model", modelName);
    }

    [Fact]
    public void DeriveClassNames_NoIdentifiers_ReturnsFallbackNames()
    {
        // true (no identifiers)
        var (expression, semanticModel) = CreateExpressionContext("true", "");

        var (propositionName, modelName) = ExpressionNameDeriver.DeriveClassNames(
            expression,
            semanticModel,
            0);

        Assert.Equal("Proposition", propositionName);
        Assert.Equal("Model", modelName);
    }

    [Fact]
    public void DeriveClassNames_PascalCaseConversion_ProperlyCapitalizes()
    {
        // isValid (camelCase identifier)
        var (expression, semanticModel) = CreateExpressionContext("isValid", "bool isValid");

        var (propositionName, modelName) = ExpressionNameDeriver.DeriveClassNames(
            expression,
            semanticModel,
            0);

        Assert.Equal("IsValidProposition", propositionName);
        Assert.Equal("Model", modelName);
    }

    [Fact]
    public void DeriveClassNames_NameCollision_AppendsIncrementingNumbers()
    {
        // age > 18, but AgeProposition already exists
        var (expression, semanticModel) = CreateExpressionContext(
            "age > 18",
            "int age",
            """
            public class AgeProposition { }
            public class Model { }
            """);

        var (propositionName, modelName) = ExpressionNameDeriver.DeriveClassNames(
            expression,
            semanticModel,
            0);

        Assert.Equal("AgeProposition1", propositionName);
        Assert.Equal("Model1", modelName);
    }

    [Fact]
    public void DeriveClassNames_AssignmentContext_UsesVariableName()
    {
        // var isValid = x > 5 && y < 10
        var (expression, semanticModel) = CreateAssignmentContext("isValid", "x > 5 && y < 10", "int x, int y");

        var (propositionName, modelName) = ExpressionNameDeriver.DeriveClassNames(
            expression,
            semanticModel,
            0);

        Assert.Equal("IsValidProposition", propositionName);
        Assert.Equal("Model", modelName);
    }

    [Fact]
    public void DeriveClassNames_AssignmentContext_TakesPrecedenceOverExpressionNames()
    {
        // var inRange = valueA >= 0 && valueB >= 0
        var (expression, semanticModel) = CreateAssignmentContext(
            "inRange",
            "valueA >= 0 && valueB >= 0",
            "int valueA, int valueB");

        var (propositionName, modelName) = ExpressionNameDeriver.DeriveClassNames(
            expression,
            semanticModel,
            0);

        // Should use assignment target "inRange" instead of expression variable "valueA"
        Assert.Equal("InRangeProposition", propositionName);
        Assert.Equal("Model", modelName);
    }

    [Fact]
    public void DeriveClassNames_MethodReturnContext_UsesMethodName()
    {
        // return age > 18 in IsAdult()
        var (expression, semanticModel) = CreateMethodReturnContext(
            "IsAdult",
            "age > 18",
            "int age");

        var (propositionName, modelName) = ExpressionNameDeriver.DeriveClassNames(
            expression,
            semanticModel,
            0);

        Assert.Equal("IsAdultProposition", propositionName);
        Assert.Equal("Model", modelName);
    }

    [Fact]
    public void DeriveClassNames_MethodReturnContext_TakesPrecedenceOverExpressionNames()
    {
        // return order.Total > 100 && order.IsActive in IsEligibleOrder()
        var (expression, semanticModel) = CreateMethodReturnContext(
            "IsEligibleOrder",
            "order.Total > 100 && order.IsActive",
            "Order order",
            """
            public class Order
            {
                public decimal Total { get; set; }
                public bool IsActive { get; set; }
            }
            """);

        var (propositionName, modelName) = ExpressionNameDeriver.DeriveClassNames(
            expression,
            semanticModel,
            0);

        // Should use method name "IsEligibleOrder" instead of expression variable "order"
        Assert.Equal("IsEligibleOrderProposition", propositionName);
        Assert.Equal("Model", modelName);
    }

    [Fact]
    public void DeriveClassNames_AssignmentPrecedesMethodContext()
    {
        // var isEligible = age > 18 in IsAdult() method
        var (expression, semanticModel) = CreateMethodWithAssignmentContext(
            "IsAdult",
            "isEligible",
            "age > 18",
            "int age");

        var (propositionName, modelName) = ExpressionNameDeriver.DeriveClassNames(
            expression,
            semanticModel,
            0);

        // Assignment should take precedence over method name
        Assert.Equal("IsEligibleProposition", propositionName);
        Assert.Equal("Model", modelName);
    }

    [Fact]
    public void DeriveClassNames_ConditionWithOneClause_UsesClauseMeaning()
    {
        var (expression, semanticModel) = CreateConditionContext("n > 0", "int n");

        var (propositionName, _) = ExpressionNameDeriver.DeriveClassNames(expression, semanticModel, 0);

        Assert.Equal("IsNPositiveProposition", propositionName);
    }

    [Fact]
    public void DeriveClassNames_ConditionWithTwoClauses_JoinsClauseMeaningsSharingTheirSubject()
    {
        var (expression, semanticModel) = CreateConditionContext("n > 0 && n < 10", "int n");

        var (propositionName, _) = ExpressionNameDeriver.DeriveClassNames(expression, semanticModel, 0);

        Assert.Equal("IsNPositiveAndLessThan10Proposition", propositionName);
    }

    [Fact]
    public void DeriveClassNames_ConditionWithTwoClausesOnDifferentSubjects_JoinsWholeClauseMeanings()
    {
        var (expression, semanticModel) = CreateConditionContext("x > 0 || y > 0", "int x, int y");

        var (propositionName, _) = ExpressionNameDeriver.DeriveClassNames(expression, semanticModel, 0);

        Assert.Equal("IsXPositiveOrIsYPositiveProposition", propositionName);
    }

    [Fact]
    public void DeriveClassNames_ConditionWithMoreThanTwoClauses_UsesEnclosingMemberName()
    {
        var (expression, semanticModel) = CreateConditionContext("x > 0 && y > 0 && x < y", "int x, int y");

        var (propositionName, _) = ExpressionNameDeriver.DeriveClassNames(expression, semanticModel, 0);

        Assert.Equal("ClampProposition", propositionName);
    }

    [Theory]
    [InlineData("((n > 0 && n < 10))", "int n", "IsNPositiveAndLessThan10Proposition")]
    [InlineData("x > 0 ^ y > 0", "int x, int y", "ClampProposition")]
    [InlineData("firstMeasurement > 0 && secondMeasurement > 0", "int firstMeasurement, int secondMeasurement", "ClampProposition")]
    [InlineData("n > 0 && theFirstRatherLongMeasurementName > theSecondRatherLongMeasurementName", "int n, int theFirstRatherLongMeasurementName, int theSecondRatherLongMeasurementName", "ClampProposition")]
    [InlineData("theFirstRatherLongMeasurementName > theSecondRatherLongMeasurementName && n > 0", "int n, int theFirstRatherLongMeasurementName, int theSecondRatherLongMeasurementName", "ClampProposition")]
    public void DeriveClassNames_ConditionThatClauseMeaningCannotName_FallsBackToEnclosingMember(
        string condition,
        string parameterList,
        string expectedName)
    {
        var (expression, semanticModel) = CreateConditionContext(condition, parameterList);

        var (propositionName, _) = ExpressionNameDeriver.DeriveClassNames(expression, semanticModel, 0);

        Assert.Equal(expectedName, propositionName);
    }

    [Fact]
    public void DeriveClassNames_ConditionInLocalFunction_UsesLocalFunctionName()
    {
        var source = """
            public class TestClass
            {
                public void TestMethod()
                {
                    void Validate(int x, int y)
                    {
                        if (x > 0 && y > 0 && x < y)
                        {
                        }
                    }
                }
            }
            """;
        var (expression, semanticModel) = CreateContext(source, root => root
            .DescendantNodes()
            .OfType<IfStatementSyntax>()
            .First()
            .Condition);

        var (propositionName, _) = ExpressionNameDeriver.DeriveClassNames(expression, semanticModel, 0);

        Assert.Equal("ValidateProposition", propositionName);
    }

    [Fact]
    public void DeriveClassNames_ConditionInPropertyGetter_UsesPropertyName()
    {
        var source = """
            public class TestClass
            {
                private int _x;
                private int _y;

                public string Quadrant
                {
                    get
                    {
                        if (_x > 0 && _y > 0 && _x < _y)
                            return "upper";

                        return "other";
                    }
                }
            }
            """;
        var (expression, semanticModel) = CreateContext(source, root => root
            .DescendantNodes()
            .OfType<IfStatementSyntax>()
            .First()
            .Condition);

        var (propositionName, _) = ExpressionNameDeriver.DeriveClassNames(expression, semanticModel, 0);

        Assert.Equal("QuadrantProposition", propositionName);
    }

    [Fact]
    public void DeriveClassNames_ConditionWithNoEnclosingMember_FallsBackToRootIdentifier()
    {
        var source = """
            public class TestClass
            {
                public TestClass(int count)
                {
                    if (count > 0 && count < 10 && count != 5)
                    {
                    }
                }
            }
            """;
        var (expression, semanticModel) = CreateContext(source, root => root
            .DescendantNodes()
            .OfType<IfStatementSyntax>()
            .First()
            .Condition);

        var (propositionName, _) = ExpressionNameDeriver.DeriveClassNames(expression, semanticModel, 0);

        Assert.Equal("CountProposition", propositionName);
    }

    [Fact]
    public void DeriveClassNames_ExpressionBodiedProperty_UsesPropertyName()
    {
        var source = """
            public class TestClass
            {
                private int _count;

                public bool IsInRange => _count > 0 && _count < 10;
            }
            """;
        var (expression, semanticModel) = CreateContext(source, root => root
            .DescendantNodes()
            .OfType<ArrowExpressionClauseSyntax>()
            .First()
            .Expression);

        var (propositionName, _) = ExpressionNameDeriver.DeriveClassNames(expression, semanticModel, 0);

        Assert.Equal("IsInRangeProposition", propositionName);
    }

    [Fact]
    public void DeriveClassNames_UnderscorePrefixedField_DropsTheUnderscore()
    {
        var source = """
            public class TestClass
            {
                private int _count;

                public bool TestMethod()
                {
                    return _count > 0;
                }
            }
            """;
        var (expression, semanticModel) = CreateContext(source, root => root
            .DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .First()
            .Expression!);

        var (propositionName, _) = ExpressionNameDeriver.DeriveClassNames(expression, semanticModel, 0);

        Assert.Equal("CountProposition", propositionName);
    }

    /// <summary>
    /// Helper method to create an <c>if</c> condition context, with no assignment or return to name it after.
    /// </summary>
    private static (ExpressionSyntax Expression, SemanticModel SemanticModel) CreateConditionContext(
        string expression,
        string parameterList)
    {
        var source = $$"""
            public class TestClass
            {
                public void Clamp({{parameterList}})
                {
                    if ({{expression}})
                    {
                    }
                }
            }
            """;

        return CreateContext(source, root => root
            .DescendantNodes()
            .OfType<IfStatementSyntax>()
            .First()
            .Condition);
    }

    private static (ExpressionSyntax Expression, SemanticModel SemanticModel) CreateContext(
        string source,
        Func<SyntaxNode, ExpressionSyntax> findExpression)
    {
        var tree = CSharpSyntaxTree.ParseText(source);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { tree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return (findExpression(tree.GetRoot()), compilation.GetSemanticModel(tree));
    }

    /// <summary>
    /// Helper method to create a compilable expression context for testing.
    /// </summary>
    private static (ExpressionSyntax Expression, SemanticModel SemanticModel) CreateExpressionContext(
        string expression,
        string parameterList,
        string additionalCode = "")
    {
        var source = $$"""
            {{additionalCode}}

            public class TestClass
            {
                public bool TestMethod({{parameterList}})
                {
                    return {{expression}};
                }
            }
            """;

        return CreateContext(source, root => root
            .DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .First()
            .Expression!);
    }

    /// <summary>
    /// Helper method to create an assignment context for testing.
    /// </summary>
    private static (ExpressionSyntax Expression, SemanticModel SemanticModel) CreateAssignmentContext(
        string variableName,
        string expression,
        string parameterList,
        string additionalCode = "")
    {
        var source = $$"""
            {{additionalCode}}

            public class TestClass
            {
                public void TestMethod({{parameterList}})
                {
                    var {{variableName}} = {{expression}};
                }
            }
            """;

        return CreateContext(source, root => root
            .DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .First(v => v.Identifier.ValueText == variableName)
            .Initializer!
            .Value);
    }

    /// <summary>
    /// Helper method to create a method return context for testing.
    /// </summary>
    private static (ExpressionSyntax Expression, SemanticModel SemanticModel) CreateMethodReturnContext(
        string methodName,
        string expression,
        string parameterList,
        string additionalCode = "")
    {
        var source = $$"""
            {{additionalCode}}

            public class TestClass
            {
                public bool {{methodName}}({{parameterList}})
                {
                    return {{expression}};
                }
            }
            """;

        return CreateContext(source, root => root
            .DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .First()
            .Expression!);
    }

    /// <summary>
    /// Helper method to create a method with assignment context for testing precedence.
    /// </summary>
    private static (ExpressionSyntax Expression, SemanticModel SemanticModel) CreateMethodWithAssignmentContext(
        string methodName,
        string variableName,
        string expression,
        string parameterList,
        string additionalCode = "")
    {
        var source = $$"""
            {{additionalCode}}

            public class TestClass
            {
                public bool {{methodName}}({{parameterList}})
                {
                    var {{variableName}} = {{expression}};
                    return {{variableName}};
                }
            }
            """;

        return CreateContext(source, root => root
            .DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .First(v => v.Identifier.ValueText == variableName)
            .Initializer!
            .Value);
    }
}
