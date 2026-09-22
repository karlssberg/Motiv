using Shouldly;
using Xunit;

namespace Motiv.Serialization.Tests.Printing;

/// <summary>The names a document uses, as the identifiers and type names C# accepts.</summary>
public class CSharpIdentifiersTests
{
    [Theory]
    [InlineData("2fa-check", "_2faCheck")]
    [InlineData("can-checkout", "CanCheckout")]
    [InlineData("", "_")]
    [InlineData("---", "_")]
    public void Should_make_a_legal_type_name_of_any_rule_name(string name, string expected)
    {
        CSharpIdentifiers.TypeName(name).ShouldBe(expected);
    }

    [Theory]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(long), "long")]
    [InlineData(typeof(short), "short")]
    [InlineData(typeof(byte), "byte")]
    [InlineData(typeof(sbyte), "sbyte")]
    [InlineData(typeof(uint), "uint")]
    [InlineData(typeof(ulong), "ulong")]
    [InlineData(typeof(ushort), "ushort")]
    [InlineData(typeof(bool), "bool")]
    [InlineData(typeof(string), "string")]
    [InlineData(typeof(char), "char")]
    [InlineData(typeof(double), "double")]
    [InlineData(typeof(float), "float")]
    [InlineData(typeof(decimal), "decimal")]
    [InlineData(typeof(object), "object")]
    [InlineData(typeof(Order), "Order")]
    public void Should_name_a_framework_primitive_by_its_keyword_and_anything_else_by_its_name(Type type, string expected)
    {
        CSharpIdentifiers.TypeName(type).ShouldBe(expected);
    }

    [Theory]
    [InlineData("is-active", "isActive")]
    [InlineData("class", "@class")]
    [InlineData("2fa", "_2fa")]
    [InlineData("", "_")]
    public void Should_make_a_legal_parameter_name(string name, string expected)
    {
        CSharpIdentifiers.CamelCase(name).ShouldBe(expected);
    }

    private sealed record Order(decimal Total);
}
