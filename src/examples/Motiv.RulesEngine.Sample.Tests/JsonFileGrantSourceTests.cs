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

    [Fact]
    public void Should_normalize_verb_casing_on_add_so_remove_matches_by_equality()
    {
        // Arrange — LadderVerb/IsAdministerRow read the Verb case-insensitively, but Remove
        // matches by record equality, so a mixed-case Verb must be normalized before it is stored.
        var source = new JsonFileGrantSource(TempPath());
        source.Add(new GrantRecord("alice", "pricing", "Author"));

        // Assert — stored lowercase, regardless of how it was added
        source.All.ShouldContain(new GrantRecord("alice", "pricing", "author"));

        // Act & Assert — a lowercase-verb record (what a caller reads back from All) matches
        source.Remove(new GrantRecord("alice", "pricing", "author")).ShouldBe(GrantRemovalOutcome.Removed);
    }

    [Fact]
    public void Should_normalize_verb_casing_loaded_from_a_hand_edited_file()
    {
        // Arrange — a file written outside JsonFileGrantSource is the other way mixed casing arrives
        var path = TempPath();
        File.WriteAllText(path, """[{"subject":"alice","prefix":"pricing","verb":"Author"}]""");

        // Act
        var source = new JsonFileGrantSource(path);

        // Assert
        source.All.ShouldContain(new GrantRecord("alice", "pricing", "author"));
        source.Remove(new GrantRecord("alice", "pricing", "author")).ShouldBe(GrantRemovalOutcome.Removed);
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"grants-{Guid.NewGuid():N}.json");

    private static ClaimsPrincipal Principal(string subject) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, subject)], "test"));
}
