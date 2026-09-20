using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

public class SchemaTests
{
    [Fact]
    public async Task Should_create_the_three_tables()
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
}
