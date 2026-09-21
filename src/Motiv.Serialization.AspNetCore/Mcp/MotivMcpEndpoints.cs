using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Motiv.Serialization.AspNetCore;

/// <summary>Mounts the MCP server a coding agent reaches the decision log through.</summary>
public static class MotivMcpEndpoints
{
    /// <summary>
    /// Maps the MCP endpoint (Streamable HTTP) at <paramref name="pattern"/>. Secure by default:
    /// the endpoint requires authorization as the rules API does; <paramref name="anonymous"/> is
    /// the same escape as <see cref="MotivRulesEndpointOptions.AllowAnonymous"/>, and like it holds
    /// under a host whose fallback policy requires an authenticated user.
    /// Needs <see cref="MotivRulesBuilder.AddMcp"/> to have registered the server and its tools.
    /// </summary>
    public static IEndpointConventionBuilder MapMotivMcp(
        this IEndpointRouteBuilder endpoints, string pattern = "/mcp", bool anonymous = false)
    {
        if (endpoints is null) throw new ArgumentNullException(nameof(endpoints));

        var mcp = endpoints.MapMcp(pattern);
        if (anonymous)
            mcp.AllowAnonymous();
        else
            mcp.RequireAuthorization();
        return mcp;
    }
}
