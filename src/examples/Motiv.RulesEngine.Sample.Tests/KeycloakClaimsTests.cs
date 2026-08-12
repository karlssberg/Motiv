using System.Security.Claims;
using Motiv.RulesEngine.Sample;
using Shouldly;
using Xunit;

namespace Motiv.RulesEngine.Sample.Tests;

public class KeycloakClaimsTests
{
    [Fact]
    public void Should_flatten_realm_access_roles_into_role_claims()
    {
        // Arrange
        var principal = PrincipalWithRealmAccess("""{"roles":["motiv-pricing-author","motiv-admin"]}""");

        // Act
        KeycloakClaims.FlattenRealmRoles(principal);

        // Assert
        principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe(
            new[] { "motiv-pricing-author", "motiv-admin" },
            ignoreOrder: true);
    }

    [Fact]
    public void Should_be_a_noop_when_no_realm_access_claim_is_present()
    {
        // Arrange
        var identity = new ClaimsIdentity([new Claim("sub", "someone")], "test");
        var principal = new ClaimsPrincipal(identity);

        // Act
        KeycloakClaims.FlattenRealmRoles(principal);

        // Assert
        principal.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public void Should_not_throw_and_should_be_a_noop_for_malformed_realm_access_json()
    {
        // Arrange
        var principal = PrincipalWithRealmAccess("not valid json");

        // Act
        var act = () => KeycloakClaims.FlattenRealmRoles(principal);

        // Assert
        act.ShouldNotThrow();
        principal.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public void Should_be_a_noop_when_realm_access_has_no_roles_array()
    {
        // Arrange
        var principal = PrincipalWithRealmAccess("""{"other":"value"}""");

        // Act
        KeycloakClaims.FlattenRealmRoles(principal);

        // Assert
        principal.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public void Should_preserve_existing_role_claims_and_add_flattened_ones()
    {
        // Arrange
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "already-present"),
            new Claim("realm_access", """{"roles":["motiv-admin"]}""")
        ], "test");
        var principal = new ClaimsPrincipal(identity);

        // Act
        KeycloakClaims.FlattenRealmRoles(principal);

        // Assert
        principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe(
            new[] { "already-present", "motiv-admin" },
            ignoreOrder: true);
    }

    [Fact]
    public void Should_skip_non_string_role_entries_without_throwing()
    {
        // Arrange
        var principal = PrincipalWithRealmAccess("""{"roles":[1,"motiv-admin"]}""");

        // Act
        var act = () => KeycloakClaims.FlattenRealmRoles(principal);

        // Assert
        act.ShouldNotThrow();
        principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldBe(["motiv-admin"]);
    }

    private static ClaimsPrincipal PrincipalWithRealmAccess(string realmAccessJson)
    {
        var identity = new ClaimsIdentity([new Claim("realm_access", realmAccessJson)], "test");
        return new ClaimsPrincipal(identity);
    }
}
