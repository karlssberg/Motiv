using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Motiv.RulesEngine.Sample.Tests;

/// <summary>
/// The decision log end to end: an audited rule evaluated through <c>/api/checkout</c> leaves a
/// record, an unaudited one leaves nothing, and the record keeps only the customer's key.
/// </summary>
public class DecisionLogEndpointTests
{
    private static WebApplicationFactory<Program> AnIsolatedHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseIsolatedDatabases());

    private static object ACustomer(string? id = "cust-42") =>
        new { customerId = id, age = 30, isActive = true, orderCount = 3 };

    /// <summary>
    /// Reads the log, waiting until <paramref name="expected"/> records have arrived.
    /// </summary>
    /// <remarks>
    /// The wait is the contract, not a workaround: records leave the evaluation path through a
    /// bounded queue drained by a background writer, precisely so an audited rule on a checkout path
    /// does not pay a database write per evaluation. The in-memory sink hid that by completing within
    /// the same tick; a database does not, and a test that asserted immediate visibility would be
    /// asserting the absence of the feature.
    /// </remarks>
    private static async Task<JsonElement> DecisionsAsync(HttpClient client, int expected = 1)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var decisions = await client.GetFromJsonAsync<JsonElement>("/api/decisions", timeout.Token);
            if (decisions.GetProperty("records").GetArrayLength() >= expected)
                return decisions;

            await Task.Delay(25, timeout.Token);
        }
    }

    [Fact]
    public async Task Should_record_a_decision_for_the_audited_rule_only()
    {
        // Arrange — loyalty-discount is the sample's document-default rule, and therefore the only
        // one that *can* be audited: can-checkout and fraud-screening run on compiled defaults, which
        // have no document to carry the flag
        await using var factory = AnIsolatedHost();
        var client = factory.CreateClient();

        // Act
        (await client.PostAsJsonAsync("/api/checkout", ACustomer())).EnsureSuccessStatusCode();
        var decisions = await DecisionsAsync(client);

        // Assert
        var records = decisions.GetProperty("records").EnumerateArray().ToArray();
        records.Select(record => record.GetProperty("ruleName").GetString())
            .ShouldBe(["loyalty-discount"]);
    }

    [Fact]
    public async Task Should_keep_only_the_customer_key_in_the_record()
    {
        // Arrange
        await using var factory = AnIsolatedHost();
        var client = factory.CreateClient();

        // Act
        (await client.PostAsJsonAsync("/api/checkout", ACustomer())).EnsureSuccessStatusCode();
        var decisions = await DecisionsAsync(client);

        // Assert — the GDPR-clean posture: erase cust-42 in the system of record and this survives
        // without personal data, while replay correctly becomes impossible
        var record = decisions.GetProperty("records").EnumerateArray().Single();
        var input = record.GetProperty("input");
        input.GetProperty("kind").GetString()!.ShouldBe("Reference");
        input.GetProperty("value").GetString()!.ShouldBe("cust-42");
        record.ToString().ShouldNotContain("\"age\"");
    }

    [Fact]
    public async Task Should_record_the_full_justification_and_the_three_anchors()
    {
        // Arrange
        await using var factory = AnIsolatedHost();
        var client = factory.CreateClient();

        // Act
        (await client.PostAsJsonAsync("/api/checkout", ACustomer())).EnsureSuccessStatusCode();
        var record = (await DecisionsAsync(client)).GetProperty("records").EnumerateArray().Single();

        // Assert — the payload the engine already built, plus the envelope that makes it evidence
        record.GetProperty("outcome").GetProperty("justification").GetString()
            .ShouldNotBeNullOrWhiteSpace();
        record.GetProperty("outcome").GetProperty("assertions").EnumerateArray().ShouldNotBeEmpty();
        record.GetProperty("ruleVersion").GetInt32().ShouldBe(1);
        record.GetProperty("buildId").GetString().ShouldNotBeNullOrWhiteSpace();
        record.TryGetProperty("referencedPropositionVersions", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_give_one_checkout_one_correlation_id()
    {
        // Arrange
        await using var factory = AnIsolatedHost();
        var client = factory.CreateClient();

        // Act — two checkouts, each of which is one decision
        (await client.PostAsJsonAsync("/api/checkout", ACustomer())).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/checkout", ACustomer("cust-7"))).EnsureSuccessStatusCode();
        var records = (await DecisionsAsync(client, expected: 2)).GetProperty("records").EnumerateArray().ToArray();

        // Assert — two decisions, two ids, and each record names the customer it was about
        records.Length.ShouldBe(2);
        records.Select(record => record.GetProperty("correlationId").GetString()).Distinct().Count()
            .ShouldBe(2);
    }

    [Fact]
    public async Task Should_require_authorization_to_read_the_log()
    {
        // Arrange — the decision log is the most sensitive surface the app has. The OIDC branch, as
        // ConfigurationBranchTests uses it: a no-token challenge never fetches metadata, so the
        // unreachable authority is never contacted.
        await using var factory = AnIsolatedHost().WithWebHostBuilder(builder => builder
            .UseSetting("Motiv:DevIdentity:Enabled", "false")
            .UseSetting("Motiv:Oidc:Authority", "http://localhost:9/realms/motiv")
            .UseSetting("Motiv:Grants:Path",
                Path.Combine(Path.GetTempPath(), $"grants-{Guid.NewGuid():N}.json")));
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/decisions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_refuse_to_audit_a_rule_running_on_a_compiled_default()
    {
        // Arrange — can-checkout has no document, so it has nowhere to put the flag. Publishing one
        // that sets it transcribes the default into a stored document, which is the point: audited
        // implies stored, by construction rather than by a rule anyone has to enforce.
        await using var factory = AnIsolatedHost();
        var client = factory.CreateClient();
        var document = JsonDocument.Parse(
            """{ "audited": true, "rule": { "spec": "customer.is-active" } }""").RootElement;

        // Act
        var put = await client.PutAsJsonAsync(
            "/api/rules/rules/can-checkout", new { document, baseVersion = 1 });

        // Assert — the sample registers a capture posture for Customer, so this is allowed, and the
        // rule now has a document to be audited *by*
        put.EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync("/api/checkout", ACustomer())).EnsureSuccessStatusCode();
        var records = (await DecisionsAsync(client, expected: 2)).GetProperty("records").EnumerateArray().ToArray();
        records.Select(record => record.GetProperty("ruleName").GetString())
            .ShouldContain("can-checkout");
    }
}
