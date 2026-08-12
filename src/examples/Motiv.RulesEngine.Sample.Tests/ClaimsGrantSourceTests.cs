using System.Security.Claims;
using Motiv.RulesEngine.Sample;
using Motiv.Serialization.AspNetCore;
using Shouldly;
using Xunit;

namespace Motiv.RulesEngine.Sample.Tests;

public class ClaimsGrantSourceTests
{
    [Fact]
    public void Should_grant_namespace_based_on_role_claim()
    {
        // Arrange
        var mappings = new List<ClaimsGrantMapping>
        {
            new("role", "motiv-pricing-author", "pricing", "author")
        };
        var source = new ClaimsGrantSource(mappings);
        var principal = PrincipalWithRoleClaim("motiv-pricing-author");

        // Act
        var grants = source.GrantsFor(principal);

        // Assert
        GrantEvaluator.IsGranted(grants, GrantVerb.Author, "pricing.eu").ShouldBeTrue();
    }

    [Fact]
    public void Should_not_grant_when_claim_not_held()
    {
        // Arrange
        var mappings = new List<ClaimsGrantMapping>
        {
            new("role", "motiv-pricing-author", "pricing", "author")
        };
        var source = new ClaimsGrantSource(mappings);
        var principal = PrincipalWithRoleClaim("other-role");

        // Act
        var grants = source.GrantsFor(principal);

        // Assert
        GrantEvaluator.IsGranted(grants, GrantVerb.Author, "pricing.eu").ShouldBeFalse();
    }

    [Fact]
    public void Should_identify_administrator_by_administer_verb()
    {
        // Arrange
        var mappings = new List<ClaimsGrantMapping>
        {
            new("role", "motiv-admin", "", "administer")
        };
        var source = new ClaimsGrantSource(mappings);
        var principal = PrincipalWithRoleClaim("motiv-admin");

        // Act & Assert
        source.IsAdministrator(principal).ShouldBeTrue();
    }

    [Fact]
    public void Should_exclude_administer_rows_from_grants()
    {
        // Arrange
        var mappings = new List<ClaimsGrantMapping>
        {
            new("role", "motiv-admin", "", "administer")
        };
        var source = new ClaimsGrantSource(mappings);
        var principal = PrincipalWithRoleClaim("motiv-admin");

        // Act
        var grants = source.GrantsFor(principal);

        // Assert — administer verb should not appear in namespace grants
        grants.ShouldBeEmpty();
    }

    [Fact]
    public void Should_list_all_claim_values_as_known_roles()
    {
        // Arrange
        var mappings = new List<ClaimsGrantMapping>
        {
            new("role", "motiv-pricing-author", "pricing", "author"),
            new("role", "motiv-pricing-publisher", "pricing", "publish"),
            new("role", "motiv-admin", "", "administer")
        };
        var source = new ClaimsGrantSource(mappings);

        // Act & Assert
        source.KnownRoles.ShouldBe(
            new[] { "motiv-pricing-author", "motiv-pricing-publisher", "motiv-admin" },
            ignoreOrder: true);
    }

    [Fact]
    public void Should_not_support_administration()
    {
        // Arrange
        var mappings = new List<ClaimsGrantMapping>();
        var source = new ClaimsGrantSource(mappings);

        // Act & Assert
        source.SupportsAdministration.ShouldBeFalse();
    }

    [Fact]
    public void Should_normalize_role_claim_type()
    {
        // Arrange — "role" should be normalized to ClaimTypes.Role
        var mappings = new List<ClaimsGrantMapping>
        {
            new("role", "motiv-author", "content", "author")
        };
        var source = new ClaimsGrantSource(mappings);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Role, "motiv-author") },
            "test"));

        // Act
        var grants = source.GrantsFor(principal);

        // Assert
        GrantEvaluator.IsGranted(grants, GrantVerb.Author, "content").ShouldBeTrue();
    }

    [Fact]
    public void Should_handle_multiple_role_claims()
    {
        // Arrange
        var mappings = new List<ClaimsGrantMapping>
        {
            new("role", "motiv-pricing-author", "pricing", "author"),
            new("role", "motiv-inventory-read", "inventory", "read")
        };
        var source = new ClaimsGrantSource(mappings);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] {
                new Claim(ClaimTypes.Role, "motiv-pricing-author"),
                new Claim(ClaimTypes.Role, "motiv-inventory-read")
            },
            "test"));

        // Act
        var grants = source.GrantsFor(principal);

        // Assert
        GrantEvaluator.IsGranted(grants, GrantVerb.Author, "pricing").ShouldBeTrue();
        GrantEvaluator.IsGranted(grants, GrantVerb.Read, "inventory").ShouldBeTrue();
    }

    [Fact]
    public void Should_throw_for_unknown_verb()
    {
        // Arrange
        var mappings = new List<ClaimsGrantMapping>
        {
            new("role", "some-role", "namespace", "unknown-verb")
        };

        // Act & Assert
        Should.Throw<ArgumentException>(() => new ClaimsGrantSource(mappings));
    }

    [Fact]
    public void Should_match_custom_claim_types_like_groups()
    {
        // Arrange — a mapping with claimType "groups" should match "groups" claims, not role claims
        var mappings = new List<ClaimsGrantMapping>
        {
            new("groups", "pricing-team", "pricing", "author")
        };
        var source = new ClaimsGrantSource(mappings);

        // Act & Assert — principal with matching "groups" claim should get the grant
        var principalWithGroupsClaim = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("groups", "pricing-team") },
            "test"));
        var grantsWithGroup = source.GrantsFor(principalWithGroupsClaim);
        GrantEvaluator.IsGranted(grantsWithGroup, GrantVerb.Author, "pricing").ShouldBeTrue();

        // Act & Assert — principal with only role claim should NOT get the grant
        var principalWithOnlyRoleClaim = PrincipalWithRoleClaim("pricing-team");
        var grantsWithoutGroup = source.GrantsFor(principalWithOnlyRoleClaim);
        GrantEvaluator.IsGranted(grantsWithoutGroup, GrantVerb.Author, "pricing").ShouldBeFalse();
    }

    private static ClaimsPrincipal PrincipalWithRoleClaim(string roleValue) =>
        new(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Role, roleValue) },
            "test"));
}
