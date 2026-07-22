using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Motiv.Serialization.AspNetCore.Tests;

public class RuleEndpointTests
{
    private sealed record Customer(bool IsActive);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class ActiveRule() : Rule<Customer, string>("active-rule", IsActive, "the demo rule");

    private static (SpecRegistry, MotivRulesOptions, RuleSet) Fixture()
    {
        var registry = new SpecRegistry().Register("is-active", IsActive);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");
        var rules = new RuleSet(registry).Add(new ActiveRule());
        return (registry, options, rules);
    }

    [Fact]
    public async Task Should_list_rules()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();

        // Act
        var body = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules");

        // Assert
        var entry = body[0];
        entry.GetProperty("name").GetString()!.ShouldBe("active-rule");
        entry.GetProperty("modelType").GetString()!.ShouldBe("customer");
        entry.GetProperty("metadataType").GetString()!.ShouldBe("String");
        entry.GetProperty("isAsync").GetBoolean().ShouldBeFalse();
        entry.GetProperty("isPolicy").GetBoolean().ShouldBeFalse();
        entry.GetProperty("version").GetInt32().ShouldBe(1);
        entry.GetProperty("description").GetString()!.ShouldBe("the demo rule");
    }

    [Fact]
    public async Task Should_get_a_rule_with_a_null_document_while_on_a_compiled_default()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();

        // Act
        var body = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/active-rule");

        // Assert
        body.GetProperty("version").GetInt32().ShouldBe(1);
        body.GetProperty("document").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_put_a_document_and_serve_it_back()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();
        var document = JsonDocument.Parse("""{ "rule": { "not": { "spec": "is-active" } } }""").RootElement;

        // Act
        var put = await client.PutAsJsonAsync("/api/rules/rules/active-rule",
            new { document, baseVersion = 1 });

        // Assert
        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt32().ShouldBe(2);

        var get = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/active-rule");
        get.GetProperty("version").GetInt32().ShouldBe(2);
        get.GetProperty("document").GetProperty("rule").GetProperty("not").GetProperty("spec")
            .GetString()!.ShouldBe("is-active");
    }

    [Fact]
    public async Task Should_409_with_the_current_version_on_a_stale_base_version()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();
        var document = JsonDocument.Parse("""{ "rule": { "spec": "is-active" } }""").RootElement;
        await client.PutAsJsonAsync("/api/rules/rules/active-rule", new { document, baseVersion = 1 });

        // Act — a second writer still on version 1
        var conflict = await client.PutAsJsonAsync("/api/rules/rules/active-rule", new { document, baseVersion = 1 });

        // Assert
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("currentVersion").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Should_400_with_structured_errors_for_an_invalid_document()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();
        var document = JsonDocument.Parse("""{ "rule": { "spec": "missing" } }""").RootElement;

        // Act
        var response = await client.PutAsJsonAsync("/api/rules/rules/active-rule", new { document, baseVersion = 1 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.GetProperty("errors")[0];
        error.GetProperty("code").GetString()!.ShouldBe("UnknownSpec");
        error.GetProperty("path").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_400_for_a_put_without_a_document()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();

        // Act — no document property at all
        var response = await client.PutAsJsonAsync("/api/rules/rules/active-rule", new { baseVersion = 1 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString()!.ShouldContain("document");
    }

    [Fact]
    public async Task Should_400_for_a_put_with_a_non_positive_base_version()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();
        var document = JsonDocument.Parse("""{ "rule": { "spec": "is-active" } }""").RootElement;

        // Act — versions start at 1, so 0 can never be a real observed version
        var response = await client.PutAsJsonAsync("/api/rules/rules/active-rule", new { document, baseVersion = 0 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString()!.ShouldContain("baseVersion");
    }

    [Fact]
    public async Task Should_400_for_a_delete_with_a_non_positive_base_version()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();

        // Act — versions start at 1, so 0 can never be a real observed version
        var response = await client.DeleteAsync("/api/rules/rules/active-rule?baseVersion=0");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString()!.ShouldContain("baseVersion");
    }

    [Fact]
    public async Task Should_404_for_unknown_rules()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();

        // Act & Assert
        (await client.GetAsync("/api/rules/rules/nope")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_404_for_a_put_or_delete_on_an_unknown_rule()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();
        var document = JsonDocument.Parse("""{ "rule": { "spec": "is-active" } }""").RootElement;

        // Act
        var put = await client.PutAsJsonAsync("/api/rules/rules/nope", new { document, baseVersion = 1 });
        var delete = await client.DeleteAsync("/api/rules/rules/nope?baseVersion=1");

        // Assert
        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_409_for_a_delete_with_a_stale_base_version()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();

        // Act — the rule is at version 1, not 2
        var response = await client.DeleteAsync("/api/rules/rules/active-rule?baseVersion=2");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("currentVersion").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Should_revert_via_delete_and_bump_the_version()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();
        var document = JsonDocument.Parse("""{ "rule": { "not": { "spec": "is-active" } } }""").RootElement;
        await client.PutAsJsonAsync("/api/rules/rules/active-rule", new { document, baseVersion = 1 });

        // Act
        var response = await client.DeleteAsync("/api/rules/rules/active-rule?baseVersion=2");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var get = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/active-rule");
        get.GetProperty("version").GetInt32().ShouldBe(3);
        get.GetProperty("document").ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
