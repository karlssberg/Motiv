#if NET8_0_OR_GREATER && !MOTIV_NETSTANDARD_ASSET
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Motiv.Serialization.Tests.Expressions;

/// <summary>
/// An enum reads in a leaf the way the host serializes it — through
/// <see cref="RuleSerializerOptions.ModelJsonOptions" /> as well as through attributes — because
/// that is what the catalog's schema publishes and what the editor's checker types against.
/// </summary>
public class LeafEnumPresentationTests
{
    public enum Channel { Retail, Wholesale }

    public enum Priority { Standard, NextDay }

    public sealed record Shipment(Channel Channel, Priority Priority, Channel? MaybeChannel);

    public sealed record Override([property: JsonConverter(typeof(JsonStringEnumConverter))] Priority Priority);

    private static readonly Shipment Sample = new(Channel.Wholesale, Priority.NextDay, Channel.Retail);

    private static RuleSerializer Serializer(JsonSerializerOptions? modelJson) =>
        new(new SpecRegistry(), new RuleSerializerOptions { ModelJsonOptions = modelJson });

    private static JsonSerializerOptions ByName(JsonNamingPolicy? policy = null) =>
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(policy) } };

    private static string Leaf(string expression) =>
        $$"""{ "rule": { "expression": {{JsonSerializer.Serialize(expression)}} } }""";

    [Fact]
    public void Should_compare_an_enum_by_name_when_the_options_register_a_string_enum_converter()
    {
        var serializer = Serializer(ByName());

        serializer.Validate<Shipment>(Leaf("channel == \"Wholesale\"")).ShouldBeEmpty();
        serializer.Deserialize<Shipment>(Leaf("channel == \"Wholesale\"")).Evaluate(Sample).Satisfied.ShouldBeTrue();
        serializer.Deserialize<Shipment>(Leaf("channel == \"Retail\"")).Evaluate(Sample).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_refuse_a_number_for_an_enum_the_options_serialize_by_name()
    {
        var error = Serializer(ByName()).Validate<Shipment>(Leaf("channel == 1")).ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ExpressionTypeMismatch);
    }

    [Fact]
    public void Should_compare_by_the_names_a_naming_policy_writes()
    {
        var serializer = Serializer(ByName(JsonNamingPolicy.CamelCase));

        serializer.Validate<Shipment>(Leaf("priority == \"nextDay\"")).ShouldBeEmpty();
        serializer.Deserialize<Shipment>(Leaf("priority == \"nextDay\"")).Evaluate(Sample).Satisfied.ShouldBeTrue();
        serializer.Deserialize<Shipment>(Leaf("priority != \"standard\"")).Evaluate(Sample).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_refuse_the_clr_name_when_a_naming_policy_rewrites_it()
    {
        var error = Serializer(ByName(JsonNamingPolicy.CamelCase)).Validate<Shipment>(Leaf("priority == \"NextDay\"")).ShouldHaveSingleItem();
        error.Code.ShouldBe(RuleErrorCode.ExpressionTypeMismatch);
        error.Message.ShouldContain("\"nextDay\"");
        error.Message.ShouldNotContain("\"NextDay\"", Case.Sensitive);
    }

    [Fact]
    public void Should_present_a_nullable_enum_by_name_through_the_options()
    {
        var serializer = Serializer(ByName(JsonNamingPolicy.CamelCase));

        serializer.Deserialize<Shipment>(Leaf("maybeChannel == \"retail\"")).Evaluate(Sample).Satisfied.ShouldBeTrue();
        serializer.Deserialize<Shipment>(Leaf("maybeChannel == \"retail\"")).Evaluate(Sample with { MaybeChannel = null }).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_honour_a_converter_registered_for_one_enum_alone()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter<Priority>(JsonNamingPolicy.KebabCaseLower) },
        };
        var serializer = Serializer(options);

        serializer.Deserialize<Shipment>(Leaf("priority == \"next-day\"")).Evaluate(Sample).Satisfied.ShouldBeTrue();
        serializer.Deserialize<Shipment>(Leaf("channel == 1")).Evaluate(Sample).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_compare_by_number_when_neither_the_options_nor_an_attribute_name_the_enum()
    {
        var serializer = Serializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        serializer.Deserialize<Shipment>(Leaf("channel == 1")).Evaluate(Sample).Satisfied.ShouldBeTrue();
        serializer.Validate<Shipment>(Leaf("channel == \"Wholesale\"")).ShouldHaveSingleItem()
            .Code.ShouldBe(RuleErrorCode.ExpressionTypeMismatch);
    }

    [Fact]
    public void Should_compare_by_number_without_model_options_when_no_attribute_names_the_enum()
    {
        Serializer(null).Deserialize<Shipment>(Leaf("channel == 1")).Evaluate(Sample).Satisfied.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, "model.Priority.ToString() == \"NextDay\"", "priority == \"NextDay\"")]
    [InlineData("camel", "model.Priority.ToJsonName() == \"nextDay\"", "priority == \"nextDay\"")]
    public void Should_explain_the_comparison_with_the_clause_a_developer_would_read(string? policy, string assertion, string leaf)
    {
        var serializer = Serializer(ByName(policy is null ? null : JsonNamingPolicy.CamelCase));

        serializer.Deserialize<Shipment>(Leaf(leaf)).Evaluate(Sample).Assertions.ShouldBe([assertion]);
    }

    [Fact]
    public void Should_evaluate_an_undefined_value_without_throwing_even_when_the_converter_refuses_numbers()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
        };
        var spec = Serializer(options).Deserialize<Shipment>(Leaf("priority != \"nextDay\""));

        spec.Evaluate(Sample with { Priority = (Priority)7 }).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_let_a_member_attribute_outrank_the_options_converter()
    {
        var serializer = Serializer(ByName(JsonNamingPolicy.CamelCase));

        serializer.Validate<Override>(Leaf("priority == \"NextDay\"")).ShouldBeEmpty();
        serializer.Deserialize<Override>(Leaf("priority == \"NextDay\"")).Evaluate(new Override(Priority.NextDay)).Satisfied.ShouldBeTrue();
    }
}
#endif
