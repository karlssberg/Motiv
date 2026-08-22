using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Motiv.Serialization.AspNetCore;
using Shouldly;
using Xunit;

namespace Motiv.RulesEngine.Sample.Tests;

public class BootstrapGrantSourceTests
{
    private const string BootstrapSubject = "bootstrap-user";

    [Fact]
    public void Should_treat_the_bootstrap_subject_as_administrator_while_the_store_is_empty()
    {
        // Arrange — a fresh, empty app store with no administer row at all
        var inner = new JsonFileGrantSource(TempPath());
        var source = new BootstrapGrantSource(inner, BootstrapSubject);

        // Act & Assert
        source.IsAdministrator(Principal(BootstrapSubject)).ShouldBeTrue();
    }

    [Fact]
    public void Should_not_elevate_any_other_subject_while_the_store_is_empty()
    {
        // Arrange
        var inner = new JsonFileGrantSource(TempPath());
        var source = new BootstrapGrantSource(inner, BootstrapSubject);

        // Act & Assert
        source.IsAdministrator(Principal("someone-else")).ShouldBeFalse();
    }

    [Fact]
    public void Should_go_inert_once_a_real_administer_grant_exists()
    {
        // Arrange — a real admin ("root") is seeded directly on the inner store
        var inner = new JsonFileGrantSource(TempPath());
        var source = new BootstrapGrantSource(inner, BootstrapSubject);
        inner.Add(new GrantRecord("root", "", "administer"));

        // Act & Assert — the conditional seed is dead now, even for the bootstrap subject itself
        source.IsAdministrator(Principal(BootstrapSubject)).ShouldBeFalse();
        source.IsAdministrator(Principal("root")).ShouldBeTrue();
    }

    [Fact]
    public void Should_delegate_GrantsFor_KnownRoles_and_SupportsAdministration_to_the_inner_store()
    {
        // Arrange
        var inner = new JsonFileGrantSource(TempPath());
        inner.Add(new GrantRecord("alice", "pricing", "author"));
        var source = new BootstrapGrantSource(inner, BootstrapSubject);

        // Act & Assert
        source.SupportsAdministration.ShouldBe(inner.SupportsAdministration);
        source.KnownRoles.ShouldBe(inner.KnownRoles);
        GrantEvaluator.IsGranted(source.GrantsFor(Principal("alice")), GrantVerb.Author, "pricing.eu")
            .ShouldBeTrue();
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"bootstrap-grants-{Guid.NewGuid():N}.json");

    private static ClaimsPrincipal Principal(string subject) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, subject)], "test"));

    /// <summary>
    /// End-to-end proof that bootstrap elevation actually reaches the admin HTTP surface through
    /// the BootstrapGrantSource decorator, not just the in-memory type. Dev identity is off; auth
    /// comes from a minimal test scheme so the principal's subject is controlled directly, mirroring
    /// how <see cref="AdminEndpointTests"/> builds a non-default host via WithWebHostBuilder.
    /// </summary>
    public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private const string TestScheme = "Test";

        private readonly WebApplicationFactory<Program> _factory;

        public IntegrationTests(WebApplicationFactory<Program> factory) => _factory = IsolatedStore(factory);

        [Fact]
        public async Task Should_let_the_bootstrap_principal_administer_grants_through_the_decorator()
        {
            // Arrange — empty app store, bootstrap subject configured, dev identity off
            var grantsPath = TempPath();
            await using var bootstrapFactory = BuildFactory(grantsPath, BootstrapSubject);
            var client = bootstrapFactory.CreateClient();

            // Confirm the wrapper is actually what's wired, not a bare JsonFileGrantSource
            bootstrapFactory.Services.GetRequiredService<IGrantSource>()
                .ShouldBeOfType<BootstrapGrantSource>();

            // Act — the bootstrap principal, elevated only because the store is empty, seeds a
            // real administer grant for another subject through the admin API itself
            var response = await client.PostAsJsonAsync("/api/admin/grants",
                new GrantRecord("root", "", "administer"));

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Should_forbid_the_bootstrap_principal_once_a_real_administer_exists()
        {
            // Arrange — same wiring, but a real admin is seeded directly on the store before any
            // request is made, so the bootstrap principal's elevation is already inert
            var grantsPath = TempPath();
            var seed = new JsonFileGrantSource(grantsPath);
            seed.Add(new GrantRecord("root", "", "administer"));

            await using var bootstrapFactory = BuildFactory(grantsPath, BootstrapSubject);
            var client = bootstrapFactory.CreateClient();

            // Act
            var response = await client.PostAsJsonAsync("/api/admin/grants",
                new GrantRecord("alice", "pricing", "author"));

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        private WebApplicationFactory<Program> BuildFactory(string grantsPath, string authenticatedSubject) =>
            _factory.WithWebHostBuilder(builder => builder
                .UseSetting("Motiv:DevIdentity:Enabled", "false")
                .UseSetting("Motiv:Oidc:Authority", "https://issuer.invalid/")
                .UseSetting("Motiv:Oidc:Audience", "motiv-tests")
                .UseSetting("Motiv:Grants:Source", "app")
                .UseSetting("Motiv:Grants:Path", grantsPath)
                .UseSetting("Motiv:Bootstrap:Subject", BootstrapSubject)
                .ConfigureServices(services => services
                    .AddAuthentication(TestScheme)
                    .AddScheme<TestSchemeOptions, TestAuthHandler>(TestScheme, o => o.Subject = authenticatedSubject)));

        // Points the store at a fresh temp database rather than the sample's real motiv-store.db,
        // which every WebApplicationFactory<Program> in this assembly (and `dotnet run` itself)
        // shares on disk. StoreSchema now survives two hosts creating the schema at once, so this
        // is no longer about that crash: xunit runs test classes in parallel, and a shared store
        // would let one class's published rules and grants show up in another's assertions.
        private static WebApplicationFactory<Program> IsolatedStore(WebApplicationFactory<Program> factory) =>
            factory.WithWebHostBuilder(builder => builder.UseSetting(
                "Motiv:Store:ConnectionString",
                $"Data Source={Path.Combine(Path.GetTempPath(), $"motiv-{Guid.NewGuid():N}.db")}"));

        private sealed class TestSchemeOptions : AuthenticationSchemeOptions
        {
            public string Subject { get; set; } = "unset";
        }

        /// <summary>Authenticates every request as a fixed subject via NameIdentifier — a stand-in
        /// for real OIDC token validation, which integration tests here have no need to exercise.</summary>
        private sealed class TestAuthHandler(
            IOptionsMonitor<TestSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : AuthenticationHandler<TestSchemeOptions>(options, logger, encoder)
        {
            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                var identity = new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, Options.Subject)], TestScheme);
                var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
        }
    }
}
