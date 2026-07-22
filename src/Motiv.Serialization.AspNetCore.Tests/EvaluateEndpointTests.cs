using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Motiv.Serialization.AspNetCore.Tests;

public class EvaluateEndpointTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0).WhenTrue("is positive").WhenFalse("is not positive").Create();

    private static async Task<WebApplication> StartAsync()
    {
        var registry = new SpecRegistry().Register("is-positive", IsPositive);
        var options = new MotivRulesOptions().AddModel<int>("number");
        return await TestApp.StartAsync(registry, options);
    }

    [Fact]
    public async Task Should_evaluate_a_document_and_return_the_result_shape()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "number",
            document = JsonDocument.Parse("""{ "rule": { "spec": "is-positive" } }""").RootElement,
            model = JsonDocument.Parse("5").RootElement
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/evaluate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("satisfied").GetBoolean().ShouldBeTrue();
        body.GetProperty("reason").GetString()!.ShouldBe("is positive");
        body.GetProperty("assertions")[0].GetString()!.ShouldBe("is positive");
        body.GetProperty("explanation").GetProperty("assertions")[0].GetString()!.ShouldBe("is positive");
    }

    [Fact]
    public async Task Should_reflect_a_false_outcome()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "number",
            document = JsonDocument.Parse("""{ "rule": { "spec": "is-positive" } }""").RootElement,
            model = JsonDocument.Parse("-5").RootElement
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/evaluate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("satisfied").GetBoolean().ShouldBeFalse();
        body.GetProperty("reason").GetString()!.ShouldBe("is not positive");
    }

    [Fact]
    public async Task Should_return_400_with_errors_for_an_invalid_document()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "number",
            document = JsonDocument.Parse("""{ "rule": { "spec": "does-not-exist" } }""").RootElement,
            model = JsonDocument.Parse("5").RootElement
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/evaluate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors")[0].GetProperty("code").GetString()!.ShouldBe("UnknownSpec");
    }

    [Fact]
    public async Task Should_return_400_for_a_missing_document()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "number",
            model = JsonDocument.Parse("5").RootElement
        };

        // Act — no document property at all
        var response = await client.PostAsJsonAsync("/api/rules/evaluate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString()!.ShouldContain("document");
    }

    [Fact]
    public async Task Should_return_400_for_a_missing_model()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "number",
            document = JsonDocument.Parse("""{ "rule": { "spec": "is-positive" } }""").RootElement
        };

        // Act — no model property at all (distinct from an explicit JSON null)
        var response = await client.PostAsJsonAsync("/api/rules/evaluate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString()!.ShouldContain("model");
    }

    [Fact]
    public async Task Should_return_400_for_an_unknown_model_type()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "nope",
            document = JsonDocument.Parse("""{ "rule": { "spec": "is-positive" } }""").RootElement,
            model = JsonDocument.Parse("5").RootElement
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/evaluate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString()!.ShouldContain("nope");
    }

    private sealed record Widget(bool Enabled);

    private static SpecBase<Widget, string> IsEnabled { get; } =
        Spec.Build((Widget w) => w.Enabled).WhenTrue("enabled").WhenFalse("disabled").Create();

    [Fact]
    public async Task Should_return_400_when_the_sample_model_shape_mismatches_the_model_type()
    {
        // Arrange
        var registry = new SpecRegistry().Register("is-enabled", IsEnabled);
        var options = new MotivRulesOptions().AddModel<Widget>("widget");
        await using var app = await TestApp.StartAsync(registry, options);
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "widget",
            document = JsonDocument.Parse("""{ "rule": { "spec": "is-enabled" } }""").RootElement,
            model = JsonDocument.Parse("\"not-an-object\"").RootElement,
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/evaluate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString()!.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_return_400_when_the_sample_model_cannot_be_bound()
    {
        // Arrange
        var registry = new SpecRegistry().Register("is-enabled", IsEnabled);
        var options = new MotivRulesOptions().AddModel<Widget>("widget");
        await using var app = await TestApp.StartAsync(registry, options);
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "widget",
            document = JsonDocument.Parse("""{ "rule": { "spec": "is-enabled" } }""").RootElement,
            model = (object?)null,
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/evaluate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString()!.ShouldNotBeNullOrWhiteSpace();
    }

    private sealed record Item(decimal Price);
    private sealed record Cart(IReadOnlyList<Item> Items);

    private static async Task<WebApplication> StartCartAppAsync()
    {
        var isPricey = Spec.Build((Item i) => i.Price >= 100m)
            .WhenTrue("pricey").WhenFalse("cheap").Create();
        var registry = new SpecRegistry()
            .Register("is-pricey", isPricey)
            .RegisterCollection<Cart, Item>("items", c => c.Items);
        var options = new MotivRulesOptions().AddModel<Cart>("cart");
        return await TestApp.StartAsync(registry, options);
    }

    [Fact]
    public async Task Should_evaluate_a_higher_order_document_over_a_collection()
    {
        // Arrange
        await using var app = await StartCartAppAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "cart",
            document = JsonDocument.Parse(
                """{ "rule": { "asAtLeastNSatisfied": { "spec": "is-pricey" }, "n": 2, "path": "items" } }""").RootElement,
            model = JsonDocument.Parse("""{ "items": [ { "price": 150 }, { "price": 200 }, { "price": 20 } ] }""").RootElement
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/evaluate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("satisfied").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_report_an_unknown_collection_path_as_a_validation_error()
    {
        // Arrange
        await using var app = await StartCartAppAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "cart",
            document = JsonDocument.Parse(
                """{ "rule": { "asAllSatisfied": { "spec": "is-pricey" }, "path": "widgets" } }""").RootElement
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/validate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors")[0].GetProperty("code").GetString()!.ShouldBe("UnknownCollection");
    }
}
