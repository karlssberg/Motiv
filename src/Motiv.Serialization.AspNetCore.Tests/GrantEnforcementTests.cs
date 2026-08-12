using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// Verifies write endpoints refuse callers lacking the appropriate namespace grant, while reads
/// stay unfiltered, and that the whole surface stays a no-op when no <see cref="IGrantSource"/>
/// is registered (the pre-existing test suite's baseline).
/// </summary>
public class GrantEnforcementTests
{
    [Fact]
    public async Task Should_refuse_publishing_a_rule_outside_the_granted_prefix_with_403()
    {
        // Arrange
        await using var app = await StartWithGrants(new NamespaceGrant("pricing", GrantVerb.Publish));

        // Act — the enrolled test rule is named "checkout.can-checkout"
        var response = await app.GetTestClient().PutAsJsonAsync(
            "/api/rules/rules/checkout.can-checkout",
            new { document = new { rule = new { spec = "customer.is-active" } }, baseVersion = 1 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).ShouldContain("publish");
    }

    [Fact]
    public async Task Should_allow_publishing_inside_the_granted_prefix()
    {
        // Arrange
        await using var app = await StartWithGrants(new NamespaceGrant("checkout", GrantVerb.Publish));

        // Act
        var response = await app.GetTestClient().PutAsJsonAsync(
            "/api/rules/rules/checkout.can-checkout",
            new { document = new { rule = new { spec = "customer.is-active" } }, baseVersion = 1 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_refuse_the_evaluate_sandbox_to_read_only_principals()
    {
        // Arrange
        await using var app = await StartWithGrants(new NamespaceGrant("", GrantVerb.Read));

        // Act
        var response = await app.GetTestClient().PostAsJsonAsync("/api/rules/evaluate",
            new { modelType = "customer", document = new { spec = "customer.is-active" }, model = new { isActive = true } });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Should_leave_reads_unfiltered()
    {
        // Arrange — no grants at all, still authenticated
        await using var app = await StartWithGrants();

        // Act
        var response = await app.GetTestClient().GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static Task<WebApplication> StartWithGrants(params NamespaceGrant[] grants)
    {
        var registry = new SpecRegistry().Register(
            "customer.is-active",
            Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create());
        var options = new MotivRulesOptions().AddModel<Customer>("customer");
        var rules = new RuleSet(registry).Add(new CanCheckoutRule());
        return TestApp.StartAsync(registry, options, rules,
            services: s => s.AddSingleton<IGrantSource>(new FakeGrantSource(grants)));
    }

    private sealed record Customer(bool IsActive);

    private sealed class CanCheckoutRule() : Rule<Customer, string>(
        "checkout.can-checkout",
        Spec.Build((Customer c) => c.IsActive).WhenTrue("ok").WhenFalse("no").Create());

    /// <summary>A fixed set of grants for one principal, reused across later tasks' tests.</summary>
    /// <param name="isAdministrator">
    /// Whether every principal is reported as an administrator, for tests of the administer-gated
    /// surfaces (e.g. gate configuration).
    /// </param>
    /// <param name="knownRoles">
    /// The role universe reported for the gate's lockout pre-check; empty unless a test needs to
    /// exercise it.
    /// </param>
    internal sealed class FakeGrantSource(
        IReadOnlyList<NamespaceGrant> grants, bool isAdministrator = false,
        IReadOnlyCollection<string>? knownRoles = null)
        : IGrantSource
    {
        /// <inheritdoc />
        public bool SupportsAdministration => false;

        /// <inheritdoc />
        public IReadOnlyCollection<string> KnownRoles => knownRoles ?? [];

        /// <inheritdoc />
        public IReadOnlyList<NamespaceGrant> GrantsFor(ClaimsPrincipal principal) => grants;

        /// <inheritdoc />
        public bool IsAdministrator(ClaimsPrincipal principal) => isAdministrator;
    }
}
