using System.Net.Http.Json;
using System.Text.Json;

namespace Motiv.Serialization.AspNetCore.Tests;

public class ExpressionEndpointTests
{
    public sealed record Order(string Status, decimal Total, int DaysSinceShipped);
    public sealed record Customer(int Age, long Points, double Score, decimal CreditLimit, IReadOnlyList<Order>? Orders);

    private static SpecBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();

    private static async Task<WebApplication> StartAsync()
    {
        var registry = new SpecRegistry().Register("is-adult", IsAdult);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");
        return await TestApp.StartAsync(registry, options);
    }

    [Fact]
    public async Task Should_stamp_numeric_formats_on_model_schemas()
    {
        await using var app = await StartAsync();
        var catalog = await app.GetTestClient().GetFromJsonAsync<JsonElement>("/api/rules/catalog");
        var customer = catalog.GetProperty("modelTypes").GetProperty("customer").GetProperty("properties");
        customer.GetProperty("age").GetProperty("format").GetString()!.ShouldBe("int32");
        customer.GetProperty("points").GetProperty("format").GetString()!.ShouldBe("int64");
        customer.GetProperty("score").GetProperty("format").GetString()!.ShouldBe("double");
        customer.GetProperty("creditLimit").GetProperty("format").GetString()!.ShouldBe("decimal");
        customer.GetProperty("orders").GetProperty("items").GetProperty("properties").GetProperty("total").GetProperty("format").GetString()!.ShouldBe("decimal");
    }

    [Fact]
    public async Task Should_return_facts_for_a_valid_leaf_and_a_range_for_an_invalid_one()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        var valid = await client.PostAsJsonAsync("/api/rules/validate", new
        {
            modelType = "customer",
            document = JsonDocument.Parse("""{ "rule": { "expression": "orders.sum(o => o.total) > 1000" } }""").RootElement,
        });
        var body = await valid.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").GetArrayLength().ShouldBe(0);
        var literal = body.GetProperty("facts").EnumerateArray().Single(f => f.GetProperty("text").GetString() == "1000");
        literal.GetProperty("type").GetString()!.ShouldBe("decimal");
        literal.GetProperty("from").GetString()!.ShouldBe("orders.sum(o => o.total)");
        literal.GetProperty("range").GetProperty("start").GetInt32().ShouldBe(27);

        var invalid = await client.PostAsJsonAsync("/api/rules/validate", new
        {
            modelType = "customer",
            document = JsonDocument.Parse("""{ "rule": { "expression": "creditLimit > score" } }""").RootElement,
        });
        var errors = (await invalid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors[0].GetProperty("code").GetString()!.ShouldBe("ExpressionTypeMismatch");
        errors[0].GetProperty("range").GetProperty("start").GetInt32().ShouldBe(0);
        errors[0].GetProperty("range").GetProperty("end").GetInt32().ShouldBe(19);
    }
}
