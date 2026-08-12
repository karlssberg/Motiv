using System.Security.Claims;
using Motiv.RulesEngine.Sample;
using Motiv.Serialization.AspNetCore;
using Shouldly;
using Xunit;

namespace Motiv.RulesEngine.Sample.Tests;

public class JsonFileGrantSourceTests
{
    [Fact]
    public void Should_grant_only_the_matching_subject()
    {
        // Arrange
        var source = new JsonFileGrantSource(TempPath());
        source.Add(new GrantRecord("alice", "pricing", "author"));

        // Act & Assert
        GrantEvaluator.IsGranted(source.GrantsFor(Principal("alice")), GrantVerb.Author, "pricing.eu").ShouldBeTrue();
        GrantEvaluator.IsGranted(source.GrantsFor(Principal("bob")), GrantVerb.Author, "pricing.eu").ShouldBeFalse();
    }

    [Fact]
    public void Should_refuse_removing_the_last_administer()
    {
        // Arrange
        var source = new JsonFileGrantSource(TempPath());
        var admin = new GrantRecord("root", "", "administer");
        source.Add(admin);

        // Act & Assert — the grant-lockout twin of gate lockout
        source.Remove(admin).ShouldBe(GrantRemovalOutcome.LastAdminister);
        source.IsAdministrator(Principal("root")).ShouldBeTrue();
    }

    [Fact]
    public void Should_persist_across_instances()
    {
        // Arrange
        var path = TempPath();
        new JsonFileGrantSource(path).Add(new GrantRecord("alice", "pricing", "publish"));

        // Act
        var reloaded = new JsonFileGrantSource(path);

        // Assert
        reloaded.All.ShouldContain(new GrantRecord("alice", "pricing", "publish"));
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"grants-{Guid.NewGuid():N}.json");

    private static ClaimsPrincipal Principal(string subject) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, subject)], "test"));
}
