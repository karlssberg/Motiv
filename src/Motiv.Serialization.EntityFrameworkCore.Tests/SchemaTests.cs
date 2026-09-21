using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

public class SchemaTests
{
    [Fact]
    public async Task Should_create_the_four_tables()
    {
        // Arrange
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        await using var context = fixture.Factory.CreateDbContext();

        // Act
        var script = context.Database.GenerateCreateScript();

        // Assert
        script.ShouldContain("MotivRuleVersion");
        script.ShouldContain("MotivPropositionVersion");
        script.ShouldContain("MotivStoreGeneration");
        script.ShouldContain("MotivScenario");
        script.ShouldNotContain("\"MotivProposition\"");
    }

    [Fact]
    public async Task Should_key_the_version_log_on_name_and_version()
    {
        // Arrange — this key is the cross-process compare-and-set, not a formality
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        await using var context = fixture.Factory.CreateDbContext();

        // Act
        var key = context.Model.FindEntityType(typeof(RuleVersionRow))!.FindPrimaryKey()!;

        // Assert
        key.Properties.Select(property => property.Name).ShouldBe(["Name", "Version"]);
    }

    [Fact]
    public async Task Should_key_the_proposition_log_on_name_and_version()
    {
        // Arrange — the same cross-process compare-and-set the rule log has
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        await using var context = fixture.Factory.CreateDbContext();

        // Act
        var key = context.Model.FindEntityType(typeof(PropositionVersionRow))!.FindPrimaryKey()!;

        // Assert
        key.Properties.Select(property => property.Name).ShouldBe(["Name", "Version"]);
    }

    [Fact]
    public async Task Should_key_scenarios_on_rule_and_id_with_version_as_the_concurrency_token()
    {
        // Arrange — rows are replaced in place, so the version is the compare-and-set here
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        await using var context = fixture.Factory.CreateDbContext();

        // Act
        var entity = context.Model.FindEntityType(typeof(ScenarioRow))!;

        // Assert
        entity.FindPrimaryKey()!.Properties.Select(p => p.Name).ShouldBe(["RuleName", "Id"]);
        entity.FindProperty("Version")!.IsConcurrencyToken.ShouldBeTrue();
    }
}
