using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Motiv.Serialization.AspNetCore;
using Shouldly;
using Xunit;

namespace Motiv.Studio.Tests;

public class GrantSourceTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Should_let_the_dev_principal_publish_anywhere_while_the_switch_is_on()
    {
        var isolated = factory.WithWebHostBuilder(builder => builder.UseIsolatedDatabases());
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
