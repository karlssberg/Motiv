using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using Shouldly;
using Xunit;

namespace Motiv.Studio.Tests;

/// <summary>Studio mounts the MCP server at <c>/mcp</c>, behind the same authentication as the rules API.</summary>
public class McpEndpointTests
{
    [Fact]
    public async Task Should_list_the_tools_an_agent_can_call()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseIsolatedDatabases());
        var http = factory.CreateClient();
        await using var mcp = await McpClient.CreateAsync(
            new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp") }, http, null, false));

        var tools = await mcp.ListToolsAsync();

        tools.Select(t => t.Name).ShouldContain("reproduce_decision");
        tools.Select(t => t.Name).ShouldContain("save_scenario");
        tools.Count.ShouldBe(7);
    }
}
