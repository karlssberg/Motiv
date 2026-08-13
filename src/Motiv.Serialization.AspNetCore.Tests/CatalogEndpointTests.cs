using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore.Tests;

public class CatalogEndpointTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0).WhenTrue("is positive").WhenFalse("is not positive").Create();

    /// <summary>Spins up a host with propositions enabled, mirroring <see cref="TestApp.StartAsync"/>
    /// for the cases that need the layered catalog (<c>EffectiveSpecs</c>) rather than the compiled
    /// one (<c>CompiledSpecs</c>).</summary>
    private static async Task<WebApplication> StartWithPropositionsAsync(
        SpecRegistry registry, MotivRulesOptions options)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuth();
        builder.Services.AddMotivRules(registry, options).AddPropositions();
        var app = builder.Build();
        app.UseTestAuth();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();
        return app;
    }

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

    [Fact]
    public async Task Should_list_declared_parameters_in_order_for_a_parameterised_spec()
    {
        // Arrange
        var registry = new SpecRegistry()
            .RegisterParameterised(
                "at-least",
                [
                    new RuleParameterDeclaration("floor", RuleParameterType.Integer, hasDefault: true, 2),
                    new RuleParameterDeclaration("label", RuleParameterType.String, hasDefault: false, null),
                ],
                values => Spec.Build((int n) => n >= (int)values["floor"]!).Create("at-least"));
        var options = new MotivRulesOptions().AddModel<int>("number");
        await using var app = await TestApp.StartAsync(registry, options);

        // Act
        var response = await app.GetTestClient().GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var parameters = body.GetProperty("specs")[0].GetProperty("parameters");

        parameters.GetArrayLength().ShouldBe(2);
        parameters[0].GetProperty("name").GetString()!.ShouldBe("floor");
        parameters[0].GetProperty("type").GetString()!.ShouldBe("integer");
        parameters[0].GetProperty("default").GetInt32().ShouldBe(2);
        parameters[1].GetProperty("name").GetString()!.ShouldBe("label");
        parameters[1].GetProperty("type").GetString()!.ShouldBe("string");
        parameters[1].GetProperty("default").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_report_no_parameters_for_a_plain_spec()
    {
        // Arrange
        var registry = new SpecRegistry().Register("is-positive", IsPositive);
        var options = new MotivRulesOptions().AddModel<int>("number");
        await using var app = await TestApp.StartAsync(registry, options);

        // Act
        var response = await app.GetTestClient().GetAsync("/api/rules/catalog");

        // Assert
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("specs")[0].GetProperty("parameters").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_surface_parameters_for_a_still_compiled_spec_in_the_layered_catalog()
    {
        // Arrange — propositions are enabled, but nothing is authored, so "at-least" resolves as
        // PropositionOrigin.Compiled through EffectiveSpecs. This proves the registry lookup inside
        // EffectiveSpecs actually finds the entry: a lookup that silently returned nothing would
        // look identical to "no parameters" and this test would fail where the plain-catalog tests
        // above (which never touch EffectiveSpecs) could not catch it.
        var registry = new SpecRegistry()
            .RegisterParameterised(
                "at-least",
                [
                    new RuleParameterDeclaration("floor", RuleParameterType.Integer, hasDefault: true, 2),
                    new RuleParameterDeclaration("label", RuleParameterType.String, hasDefault: false, null),
                ],
                values => Spec.Build((int n) => n >= (int)values["floor"]!).Create("at-least"));
        var options = new MotivRulesOptions().AddModel<int>("number");
        await using var app = await StartWithPropositionsAsync(registry, options);

        // Act
        var body = await app.GetTestClient().GetFromJsonAsync<JsonElement>("/api/rules/catalog");

        // Assert
        var atLeast = body.GetProperty("specs").EnumerateArray()
            .Single(spec => spec.GetProperty("name").GetString() == "at-least");
        atLeast.GetProperty("origin").GetString()!.ShouldBe("Compiled");

        var parameters = atLeast.GetProperty("parameters");
        parameters.GetArrayLength().ShouldBe(2);
        parameters[0].GetProperty("name").GetString()!.ShouldBe("floor");
        parameters[0].GetProperty("type").GetString()!.ShouldBe("integer");
        parameters[0].GetProperty("default").GetInt32().ShouldBe(2);
        parameters[1].GetProperty("name").GetString()!.ShouldBe("label");
        parameters[1].GetProperty("type").GetString()!.ShouldBe("string");
        parameters[1].GetProperty("default").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_suppress_parameters_when_an_override_replaces_a_parameterised_compiled_spec()
    {
        // Arrange — "at-least" is compiled as a parameterised spec, then overridden by an authored
        // document that delegates to the plain "is-positive" spec. The registry entry for
        // "at-least" still declares a parameter; the *effective* definition no longer does, because
        // its behaviour now comes from the authored document, not the argument contract. A naive
        // implementation that looks the name up in the registry and reports whatever it finds would
        // pass the sibling test above but fail this one, since the registry entry survives untouched.
        var registry = new SpecRegistry()
            .Register("is-positive", IsPositive)
            .RegisterParameterised(
                "at-least",
                [new RuleParameterDeclaration("floor", RuleParameterType.Integer, hasDefault: true, 2)],
                values => Spec.Build((int n) => n >= (int)values["floor"]!).Create("at-least"));
        var options = new MotivRulesOptions().AddModel<int>("number");
        await using var app = await StartWithPropositionsAsync(registry, options);

        var propositions = app.Services.GetRequiredService<PropositionSet>();
        var created = propositions.Create("at-least", "number", """{ "rule": { "spec": "is-positive" } }""", null);
        created.Outcome.ShouldBe(PropositionUpdateOutcome.Created);

        // Act
        var body = await app.GetTestClient().GetFromJsonAsync<JsonElement>("/api/rules/catalog");

        // Assert
        var atLeast = body.GetProperty("specs").EnumerateArray()
            .Single(spec => spec.GetProperty("name").GetString() == "at-least");
        atLeast.GetProperty("origin").GetString()!.ShouldBe("Overridden");
        atLeast.GetProperty("parameters").ValueKind.ShouldBe(JsonValueKind.Null);
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
