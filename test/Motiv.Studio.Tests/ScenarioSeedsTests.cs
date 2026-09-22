using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Motiv.Serialization;
using Motiv.Serialization.Sql;
using Shouldly;
using Xunit;

namespace Motiv.Studio.Tests;

/// <summary>
/// The seeded customers are Studio's system of record for a reproduction: a logged key that names
/// one resolves back to it through the resolver the host registers, and any other key resolves to
/// nothing.
/// </summary>
public class ScenarioSeedsTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Should_find_a_seeded_customer_by_its_id()
    {
        ScenarioSeeds.FindCustomer("cust-42", Web)!.Age.ShouldBe(30);
    }

    [Fact]
    public void Should_find_nothing_for_a_customer_typed_by_hand()
    {
        ScenarioSeeds.FindCustomer("someone-else", Web).ShouldBeNull();
    }

    [Fact]
    public async Task Should_register_a_reproducer_over_the_sql_sink_with_the_customer_resolver()
    {
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseIsolatedDatabases());
        _ = host.Server;

        host.Services.GetRequiredService<DecisionReproducer>().ShouldNotBeNull();
        host.Services.GetRequiredService<IDecisionSource>().ShouldBeSameAs(host.Services.GetRequiredService<SqlDecisionSink>());
    }
}
