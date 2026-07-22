using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Motiv.Serialization.AspNetCore.Tests;

public class ValidateEndpointTests
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
    public async Task Should_return_no_errors_for_a_valid_document()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "number",
            document = JsonDocument.Parse("""{ "rule": { "spec": "is-positive" } }""").RootElement
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/validate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_return_an_unknown_spec_error_for_a_bad_reference()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var request = new
        {
            modelType = "number",
            document = JsonDocument.Parse("""{ "rule": { "spec": "does-not-exist" } }""").RootElement
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/validate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors");
        errors.GetArrayLength().ShouldBe(1);
        errors[0].GetProperty("code").GetString()!.ShouldBe("UnknownSpec");
        errors[0].GetProperty("path").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_return_400_for_a_missing_document()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // Act — no document property at all
        var response = await client.PostAsJsonAsync("/api/rules/validate", new { modelType = "number" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString()!.ShouldContain("document");
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
            document = JsonDocument.Parse("""{ "rule": { "spec": "is-positive" } }""").RootElement
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/validate", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString()!.ShouldContain("nope");
    }
}
