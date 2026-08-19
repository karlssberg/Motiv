using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>
/// That the model maps and its DDL is producible on every supported provider. The conformance suite
/// runs on SQLite alone, which is sound only because conflict detection inspects no provider error
/// code — so what is left unproven is the schema, and this is what proves it.
/// </summary>
public class ProviderSchemaTests
{
    public static TheoryData<string, Action<DbContextOptionsBuilder>> Providers => new()
    {
        { "SQLite", builder => builder.UseSqlite("Data Source=:memory:") },
        { "PostgreSQL", builder => builder.UseNpgsql("Host=localhost;Database=motiv") },
        { "SQL Server", builder => builder.UseSqlServer("Server=localhost;Database=motiv") },
    };

    [Theory]
    [MemberData(nameof(Providers))]
    public void Should_generate_a_create_script_for_every_table(
        string provider, Action<DbContextOptionsBuilder> configure)
    {
        // Arrange — no connection is opened: script generation is a model operation
        var builder = new DbContextOptionsBuilder<MotivStoreDbContext>();
        configure(builder);
        using var context = new MotivStoreDbContext(builder.Options);

        // Act
        var script = context.Database.GenerateCreateScript();

        // Assert
        script.ShouldContain("MotivRuleVersion", customMessage: provider);
        script.ShouldContain("MotivProposition", customMessage: provider);
        script.ShouldContain("MotivStoreGeneration", customMessage: provider);
    }
}
