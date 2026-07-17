using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Motiv.Serialization.AspNetCore.Tests;

public class CatalogEndpointTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0).WhenTrue("is positive").WhenFalse("is not positive").Create();

    [Fact]
    public async Task Should_list_registered_specs_with_model_id_and_description()
    {
        // Arrange
        var registry = new SpecRegistry()
            .Register("is-positive", IsPositive, "Whether the number is positive");
        var options = new MotivRulesOptions().AddModel<int>("number");
        await using var app = await TestApp.StartAsync(registry, options);
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var entries = await response.Content.ReadFromJsonAsync<JsonElement>();
        entries.GetArrayLength().ShouldBe(1);
        var entry = entries[0];
        entry.GetProperty("name").GetString()!.ShouldBe("is-positive");
        entry.GetProperty("modelType").GetString()!.ShouldBe("number");
        entry.GetProperty("metadataType").GetString()!.ShouldBe("String");
        entry.GetProperty("isAsync").GetBoolean().ShouldBeFalse();
        entry.GetProperty("description").GetString()!.ShouldBe("Whether the number is positive");
    }
}
