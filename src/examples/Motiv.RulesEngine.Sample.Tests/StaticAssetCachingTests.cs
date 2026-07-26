using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Motiv.RulesEngine.Sample.Tests;

/// <summary>
/// The SPA shell (index.html) references content-hashed assets, so a stale cached shell points at
/// bundles that no longer exist after a redeploy. It must be revalidated on every load, while the
/// hashed assets themselves stay heuristically cacheable. The built UI is not checked in, so these
/// tests provision their own web root instead of relying on wwwroot.
/// </summary>
public sealed class StaticAssetCachingTests : IDisposable
{
    private readonly DirectoryInfo _webRoot;
    private readonly WebApplicationFactory<Program> _factory;

    public StaticAssetCachingTests()
    {
        _webRoot = Directory.CreateTempSubdirectory("motiv-sample-webroot-");
        File.WriteAllText(
            Path.Combine(_webRoot.FullName, "index.html"),
            "<!doctype html><html><head><title>test shell</title></head><body></body></html>");
        Directory.CreateDirectory(Path.Combine(_webRoot.FullName, "assets"));
        File.WriteAllText(Path.Combine(_webRoot.FullName, "assets", "index-abc123.js"), "// hashed bundle");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseWebRoot(_webRoot.FullName));
    }

    public void Dispose()
    {
        _factory.Dispose();
        _webRoot.Delete(recursive: true);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/some/client-route")]
    public async Task Should_serve_the_spa_shell_with_no_cache(string path)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(path);

        // Assert — every route that resolves to index.html must force revalidation
        response.EnsureSuccessStatusCode();
        var cacheControl = response.Headers.CacheControl.ShouldNotBeNull();
        cacheControl.NoCache.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_not_mark_hashed_assets_no_cache()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/assets/index-abc123.js");

        // Assert — content-hashed bundles are immutable per URL; leave them cacheable
        response.EnsureSuccessStatusCode();
        (response.Headers.CacheControl?.NoCache ?? false).ShouldBeFalse();
    }
}
