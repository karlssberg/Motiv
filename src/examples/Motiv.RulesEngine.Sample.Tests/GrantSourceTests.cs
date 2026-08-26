using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Motiv.Serialization.AspNetCore;
using Shouldly;
using Xunit;

namespace Motiv.RulesEngine.Sample.Tests;

public class GrantSourceTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Should_let_the_dev_principal_publish_anywhere_while_the_switch_is_on()
    {
        // Arrange — Development enables the dev identity, and with it the dev grant source. An
        // isolated store: the fixture's default points at the sample's real motiv-store.db, which
        // every WebApplicationFactory<Program> in this assembly shares on disk, and this test
        // publishes a version — leaking that into another test's assumptions is exactly the kind of
        // cross-process write the database-backed store is meant to model, just not against a shared
        // fixture's own state.
        var isolated = factory.WithWebHostBuilder(builder => builder.UseSetting(
            "Motiv:Store:ConnectionString",
            $"Data Source={Path.Combine(Path.GetTempPath(), $"motiv-{Guid.NewGuid():N}.db")}")
            .UseSetting(
                "Motiv:Decisions:ConnectionString",
                $"Data Source={Path.Combine(Path.GetTempPath(), $"motiv-decisions-{Guid.NewGuid():N}.db")}"));
        var client = isolated.CreateClient();
        var current = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/loyalty-discount");

        // First assertion: IGrantSource must be registered
        var grantSource = isolated.Services.GetRequiredService<IGrantSource>();
        grantSource.ShouldNotBeNull();

        // Act
        var response = await client.PutAsJsonAsync("/api/rules/rules/loyalty-discount", new
        {
            document = current.GetProperty("document"),
            baseVersion = current.GetProperty("version").GetInt32()
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
