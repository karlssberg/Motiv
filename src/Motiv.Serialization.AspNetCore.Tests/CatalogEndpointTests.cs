using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Motiv.Serialization.AspNetCore.Tests;

public class CatalogEndpointTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0).WhenTrue("is positive").WhenFalse("is not positive").Create();

    [Fact]
    public async Task Should_list_specs_and_collections()
    {
        // Arrange
        var registry = new SpecRegistry()
            .Register("is-positive", IsPositive, "Whether the number is positive")
            .RegisterCollection<Basket, int>("items", b => b.Items);
        var options = new MotivRulesOptions().AddModel<int>("number").AddModel<Basket>("basket");
        await using var app = await TestApp.StartAsync(registry, options);
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var spec = body.GetProperty("specs")[0];
        spec.GetProperty("name").GetString()!.ShouldBe("is-positive");
        spec.GetProperty("modelType").GetString()!.ShouldBe("number");
        spec.GetProperty("description").GetString()!.ShouldBe("Whether the number is positive");

        var collection = body.GetProperty("collections")[0];
        collection.GetProperty("path").GetString()!.ShouldBe("items");
        collection.GetProperty("parentModelType").GetString()!.ShouldBe("basket");
        collection.GetProperty("elementModelType").GetString()!.ShouldBe("number");
    }

    [Fact]
    public async Task Should_expose_metadata_type_schemas_using_the_metadata_options()
    {
        // Arrange
        var registry = new SpecRegistry()
            .Register("is-positive", IsPositive)
            .Register("has-verdict", HasVerdict);
        var options = new MotivRulesOptions().AddModel<int>("number");
        await using var app = await TestApp.StartAsync(registry, options);
        var client = app.GetTestClient();

        // Act
        var body = await client.GetFromJsonAsync<JsonElement>("/api/rules/catalog");

        // Assert
        var metadataTypes = body.GetProperty("metadataTypes");
        metadataTypes.GetProperty("String").GetRawText().ShouldBe("""{"type":["string","null"]}""");

        // Metadata payloads bind with the metadata options (STJ defaults: exact-case),
        // so the schema's property names are exact-case too.
        var verdictProperties = metadataTypes.GetProperty("Verdict").GetProperty("properties");
        verdictProperties.TryGetProperty("Code", out _).ShouldBeTrue();
        verdictProperties.TryGetProperty("code", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_expose_model_type_schemas_using_the_response_options()
    {
        // Arrange
        var registry = new SpecRegistry().Register("is-positive", IsPositive);
        var options = new MotivRulesOptions().AddModel<int>("number").AddModel<Basket>("basket");
        await using var app = await TestApp.StartAsync(registry, options);
        var client = app.GetTestClient();

        // Act
        var body = await client.GetFromJsonAsync<JsonElement>("/api/rules/catalog");

        // Assert
        var modelTypes = body.GetProperty("modelTypes");
        modelTypes.TryGetProperty("number", out _).ShouldBeTrue();

        // Models bind with the response options (web defaults: camelCase),
        // so the schema's property names are camelCase — unlike metadata schemas.
        var basketProperties = modelTypes.GetProperty("basket").GetProperty("properties");
        basketProperties.TryGetProperty("items", out _).ShouldBeTrue();
        basketProperties.TryGetProperty("Items", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_include_metadata_types_reachable_only_via_mounted_rules()
    {
        // Arrange: the Verdict metadata type appears only on the rule, not in the registry.
        var registry = new SpecRegistry().Register("is-positive", IsPositive);
        var options = new MotivRulesOptions().AddModel<int>("number");
        var rules = new RuleSet(registry).Add(new Rule<int, Verdict>("verdict-rule", HasVerdict));
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();

        // Act
        var body = await client.GetFromJsonAsync<JsonElement>("/api/rules/catalog");

        // Assert
        var metadataTypes = body.GetProperty("metadataTypes");
        metadataTypes.TryGetProperty("Verdict", out _).ShouldBeTrue();
        metadataTypes.TryGetProperty("String", out _).ShouldBeTrue();
    }

    private static SpecBase<int, Verdict> HasVerdict { get; } =
        Spec.Build((int n) => n > 0)
            .WhenTrue(new Verdict("POSITIVE"))
            .WhenFalse(new Verdict("NEGATIVE"))
            .Create("has verdict");

    private sealed record Verdict(string Code);

    private sealed class Basket
    {
        public IReadOnlyList<int> Items { get; }
        public Basket(IReadOnlyList<int> items) => Items = items;
    }
}
