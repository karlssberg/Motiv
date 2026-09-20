using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Motiv.Studio.Tests;

/// <summary>
/// The Evaluate table's four seed scenarios are written once per rule at startup and then left
/// alone: a second boot over the same store must neither duplicate them nor restore one an
/// operator removed.
/// </summary>
public class ScenarioSeedingTests
{
    [Fact]
    public async Task Should_seed_four_scenarios_per_rule_once()
    {
        // Arrange — one isolated store, booted twice
        var store = $"Data Source={Path.Combine(Path.GetTempPath(), $"motiv-seed-{Guid.NewGuid():N}.db")}";
        WebApplicationFactory<Program> Boot() => new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("Motiv:Store:ConnectionString", store)
            .UseSetting("Motiv:Decisions:ConnectionString",
                $"Data Source={Path.Combine(Path.GetTempPath(), $"motiv-seed-d-{Guid.NewGuid():N}.db")}"));

        // Act — the first boot seeds; one seed is deleted; the second boot must not put it back
        await using (var first = Boot())
        {
            var client = first.CreateClient();
            var rows = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/can-checkout/scenarios");
            rows.EnumerateArray().Select(r => r.GetProperty("name").GetString()).ShouldBe(
                ["Active adult, 3 orders", "Minor", "Dormant account", "New, no orders"]);

            var minor = rows.EnumerateArray().Single(r => r.GetProperty("name").GetString() == "Minor");
            var deleted = await client.DeleteAsync(
                $"/api/rules/rules/can-checkout/scenarios/{minor.GetProperty("id").GetString()}" +
                $"?baseVersion={minor.GetProperty("version").GetInt32()}");
            deleted.IsSuccessStatusCode.ShouldBeTrue();
        }

        await using (var second = Boot())
        {
            var rows = await second.CreateClient().GetFromJsonAsync<JsonElement>("/api/rules/rules/can-checkout/scenarios");
            rows.EnumerateArray().Select(r => r.GetProperty("name").GetString()).ShouldBe(
                ["Active adult, 3 orders", "Dormant account", "New, no orders"]);
        }
    }

    [Fact]
    public async Task Should_seed_every_registered_rule()
    {
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseIsolatedDatabases());
        var client = host.CreateClient();

        foreach (var rule in new[] { "can-checkout", "fraud-screening", "loyalty-discount" })
        {
            var rows = await client.GetFromJsonAsync<JsonElement>($"/api/rules/rules/{rule}/scenarios");
            rows.GetArrayLength().ShouldBe(4, rule);
        }
    }
}
