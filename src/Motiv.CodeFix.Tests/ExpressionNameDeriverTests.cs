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
        Assert.Equal("AgeModel", modelName);
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
        Assert.Equal("OrderModel", modelName);
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
        Assert.Equal("OrderModel", modelName);
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
        Assert.Equal("ObjModel", modelName);
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
        Assert.Equal("IsValidModel", modelName);
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
            public class AgeModel { }
            """);

        var (propositionName, modelName) = ExpressionNameDeriver.DeriveClassNames(
            expression,
            semanticModel,
            0);

        Assert.Equal("AgeProposition1", propositionName);
        Assert.Equal("AgeModel1", modelName);
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
        Assert.Equal("IsValidModel", modelName);
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
        Assert.Equal("InRangeModel", modelName);
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
        Assert.Equal("IsAdultModel", modelName);
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
        Assert.Equal("IsEligibleOrderModel", modelName);
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
        Assert.Equal("IsEligibleModel", modelName);
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

        var semanticModel = compilation.GetSemanticModel(tree);
        var returnExpr = tree.GetRoot()
            .DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .First()
            .Expression!;

        return (returnExpr, semanticModel);
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

        var semanticModel = compilation.GetSemanticModel(tree);
        var assignmentExpr = tree.GetRoot()
            .DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .First(v => v.Identifier.ValueText == variableName)
            .Initializer!
            .Value;

        return (assignmentExpr, semanticModel);
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

        var semanticModel = compilation.GetSemanticModel(tree);
        var returnExpr = tree.GetRoot()
            .DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .First()
            .Expression!;

        return (returnExpr, semanticModel);
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

        var semanticModel = compilation.GetSemanticModel(tree);
        var assignmentExpr = tree.GetRoot()
            .DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .First(v => v.Identifier.ValueText == variableName)
            .Initializer!
            .Value;

        return (assignmentExpr, semanticModel);
    }
}
