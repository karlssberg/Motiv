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

    private sealed class Basket
    {
        public IReadOnlyList<int> Items { get; }
        public Basket(IReadOnlyList<int> items) => Items = items;
    }
}
