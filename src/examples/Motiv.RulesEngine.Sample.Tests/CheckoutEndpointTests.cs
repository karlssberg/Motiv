using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Motiv.RulesEngine.Sample.Tests;

public class CheckoutEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CheckoutEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Should_approve_an_active_adult_customer()
    {
        // Arrange
        var client = _factory.CreateClient();
        var customer = new { age = 30, isActive = true, orderCount = 3, orders = new[] { new { total = 120m } } };

        // Act
        var response = await client.PostAsJsonAsync("/api/checkout", customer);

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("approved").GetBoolean().ShouldBeTrue();
        body.GetProperty("eligibility").GetProperty("satisfied").GetBoolean().ShouldBeTrue();
        body.GetProperty("screening").GetProperty("satisfied").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_reject_an_inactive_customer_with_reasons()
    {
        // Arrange
        var client = _factory.CreateClient();
        var customer = new { age = 30, isActive = false, orderCount = 0 };

        // Act
        var response = await client.PostAsJsonAsync("/api/checkout", customer);

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("approved").GetBoolean().ShouldBeFalse();
        body.GetProperty("eligibility").GetProperty("assertions").EnumerateArray()
            .Select(a => a.GetString()).ShouldContain("customer is inactive");
    }

    [Fact]
    public async Task Should_reflect_a_rule_swap_on_the_next_checkout()
    {
        // Arrange — swap can-checkout to require orders, then re-run the same customer
        await using var factory = new WebApplicationFactory<Program>();   // isolated host: mutates rule state
        var client = factory.CreateClient();
        var customer = new { age = 30, isActive = true, orderCount = 0 };
        (await (await client.PostAsJsonAsync("/api/checkout", customer))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("approved").GetBoolean().ShouldBeTrue();
        var document = JsonDocument.Parse(
            """{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "has-orders" } ] } }""").RootElement;

        // Act
        var put = await client.PutAsJsonAsync("/api/rules/rules/can-checkout", new { document, baseVersion = 1 });
        var after = await (await client.PostAsJsonAsync("/api/checkout", customer))
            .Content.ReadFromJsonAsync<JsonElement>();

        // Assert — no restart, next call sees the new rule
        put.EnsureSuccessStatusCode();
        after.GetProperty("approved").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Should_serve_the_embedded_document_default_but_null_for_a_compiled_default()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var loyalty = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/loyalty-discount");
        var canCheckout = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/can-checkout");

        // Assert — a document default is served from version 1; a compiled default has no document
        loyalty.GetProperty("version").GetInt32().ShouldBe(1);
        loyalty.GetProperty("document").ValueKind.ShouldBe(JsonValueKind.Object);
        loyalty.GetProperty("document").GetProperty("rule").GetProperty("name").GetString()
            .ShouldNotBeNull()
            .ShouldBe("qualifies for loyalty discount");
        canCheckout.GetProperty("version").GetInt32().ShouldBe(1);
        canCheckout.GetProperty("document").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_list_the_three_sample_rules()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var body = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules");

        // Assert
        var names = body.EnumerateArray().Select(r => r.GetProperty("name").GetString()).ToArray();
        names.ShouldBe(["can-checkout", "fraud-screening", "loyalty-discount"], ignoreOrder: true);
    }
}
