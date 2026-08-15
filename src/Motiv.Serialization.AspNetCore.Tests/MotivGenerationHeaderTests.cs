using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Motiv.Serialization.AspNetCore.Tests;

public class MotivGenerationHeaderTests
{
    private sealed class NumberRule() : Rule<int, string>("number", Spec.Build((int n) => n > 0).Create("positive"));

    private static SpecRegistry Registry() =>
        new SpecRegistry().Register("positive", Spec.Build((int n) => n > 0).Create("positive"));

    private static async Task<WebApplication> StartAsync()
    {
        var registry = Registry();
        var options = new MotivRulesOptions().AddModel<int>("number");
        var rules = new RuleSet(registry).Add(new NumberRule());
        return await TestApp.StartAsync(registry, options, rules);
    }

    private static async Task<StoreGeneration> ReadGeneration(HttpClient client)
    {
        var response = await client.GetAsync("/api/rules/rules");
        response.Headers.TryGetValues(MotivRulesEndpoints.GenerationHeader, out var values).ShouldBeTrue();
        StoreGeneration.TryParseToken(values!.Single(), out var generation).ShouldBeTrue();
        return generation;
    }

    [Fact]
    public async Task Should_stamp_the_generation_on_every_response()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/rules/rules");

        // Assert — the fencing token a client compares against what it has already seen
        response.Headers.TryGetValues(MotivRulesEndpoints.GenerationHeader, out var values).ShouldBeTrue();
        StoreGeneration.TryParseToken(values!.Single(), out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_stamp_the_generation_on_an_error_response_too()
    {
        // Arrange — the header matters most on the paths that refuse, where knowing which world
        // refused a caller is worth as much as knowing which world served one.
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/rules/rules/nope");

        // Assert
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
        response.Headers.TryGetValues(MotivRulesEndpoints.GenerationHeader, out var values).ShouldBeTrue();
        StoreGeneration.TryParseToken(values!.Single(), out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_move_the_stamped_generation_after_a_publish()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var before = await ReadGeneration(client);
        var document = JsonDocument.Parse("""{ "rule": { "spec": "positive" } }""").RootElement;

        // Act
        await client.PutAsJsonAsync("/api/rules/rules/number", new { document, baseVersion = 1 });
        var after = await ReadGeneration(client);

        // Assert
        after.MovedFrom(before).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_omit_the_header_on_a_registry_only_mount()
    {
        // Arrange — no RuleSet and no PropositionSet: there is no scope to pin and no generation to
        // report, so the filter must not be added at all (never throw, never stamp a placeholder).
        var registry = Registry();
        var options = new MotivRulesOptions().AddModel<int>("number");
        await using var app = await TestApp.StartAsync(registry, options);
        var client = app.GetTestClient();
        var document = JsonDocument.Parse("""{ "rule": { "spec": "positive" } }""").RootElement;

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/rules/validate", new { modelType = "number", document });

        // Assert
        response.Headers.TryGetValues(MotivRulesEndpoints.GenerationHeader, out _).ShouldBeFalse();
    }

    /// <summary>
    /// Proves the pin is real rather than merely present. A filter that stamped the header from
    /// <c>rules.PinSnapshot().Generation</c> but did not actually keep the snapshot alive across
    /// <c>next</c> — e.g. one that disposed it immediately, or that never called
    /// <see cref="BindingScope.Pin"/> at all — would still make both header tests above pass, since
    /// they only ever look at one read per request. What they cannot catch is a request that takes
    /// two reads with a real publish committed in between: without a held pin, the second read would
    /// see the write. This test forces exactly that race, deterministically, by driving the filter
    /// directly instead of over HTTP.
    /// </summary>
    [Fact]
    public async Task Should_keep_two_reads_on_one_world_even_though_a_publish_lands_between_them()
    {
        // Arrange
        var registry = Registry();
        var rules = new RuleSet(registry).Add(new NumberRule());
        var filter = new MotivGenerationFilter(rules.PinSnapshot);
        var invocationContext = EndpointFilterInvocationContext.Create(new DefaultHttpContext());
        var before = rules.FindEntry("number")!.Version;
        var reads = new List<int>();

        // Act — the publish happens *inside* the pinned request, between the two reads
        await filter.InvokeAsync(invocationContext, async _ =>
        {
            reads.Add(rules.FindEntry("number")!.Version);
            await rules.UpdateAsync(
                "number", """{"rule":{"not":{"spec":"positive"}}}""", before, new RuleChangeProvenance("test"));
            reads.Add(rules.FindEntry("number")!.Version);
            return Results.Ok();
        });

        // Assert — both reads taken inside the request see the pre-publish world
        reads[0].ShouldBe(before);
        reads[1].ShouldBe(before);

        // ...and once the pin is released (the request has ended), the world has genuinely moved
        rules.FindEntry("number")!.Version.ShouldBe(before + 1);
    }
}
