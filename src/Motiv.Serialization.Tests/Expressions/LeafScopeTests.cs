using System.Text.Json.Serialization;
using Motiv.Serialization.Expressions;

namespace Motiv.Serialization.Tests.Expressions;

public class LeafScopeTests
{
    private sealed record Order(decimal Total, [property: JsonPropertyName("shipped_days")] int DaysSinceShipped);
    private sealed record Customer(
        int Age, bool IsActive, IReadOnlyList<Order>? Orders, string? Country,
        [property: JsonIgnore] int Hidden,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int Shown);

    [Theory]
    [InlineData("age", "Age")]
    [InlineData("isActive", "IsActive")]
    [InlineData("orders", "Orders")]
    [InlineData("Age", "Age")]
    public void Should_find_members_by_their_json_name(string jsonName, string clrName)
    {
        LeafScope.FindMember(typeof(Customer), jsonName)!.Name.ShouldBe(clrName);
    }

    [Fact]
    public void Should_prefer_an_explicit_json_property_name()
    {
        LeafScope.FindMember(typeof(Order), "shipped_days")!.Name.ShouldBe("DaysSinceShipped");
        LeafScope.FindMember(typeof(Order), "daysSinceShipped").ShouldBeNull();
    }

    [Fact]
    public void Should_not_resolve_a_member_the_serializer_would_never_publish()
    {
        LeafScope.FindMember(typeof(Customer), "hidden").ShouldBeNull();
        LeafScope.FindMember(typeof(Customer), "Hidden").ShouldBeNull();
        // `Never` is the one condition that means "do not ignore", so that member stays addressable.
        LeafScope.FindMember(typeof(Customer), "shown")!.Name.ShouldBe("Shown");
    }

    [Fact]
    public void Should_return_null_for_an_unknown_name()
    {
        LeafScope.FindMember(typeof(Customer), "nope").ShouldBeNull();
    }

    [Fact]
    public void Should_find_the_element_type_of_a_collection_member()
    {
        var orders = LeafScope.FindMember(typeof(Customer), "orders")!;
        LeafScope.ElementType(LeafScope.MemberType(orders)).ShouldBe(typeof(Order));
        LeafScope.ElementType(typeof(string)).ShouldBeNull();
        LeafScope.ElementType(typeof(int)).ShouldBeNull();
    }

    [Fact]
    public void Should_carry_variables_without_mutating_the_parent()
    {
        var root = LeafScope.For(typeof(Customer), []);
        var inner = root.WithVariable("o", typeof(Order));
        inner.Variables["o"].ShouldBe(typeof(Order));
        root.Variables.ShouldBeEmpty();
        inner.ModelType.ShouldBe(typeof(Customer));
    }
}
