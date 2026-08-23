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

    [Theory]
    [MemberData(nameof(Providers))]
    public void Should_translate_the_head_projection_for_every_provider(
        string provider, Action<DbContextOptionsBuilder> configure)
    {
        // Arrange — the head projection is the store's central read path, and it must be computed
        // by the database rather than by materialising the whole append-only log
        var builder = new DbContextOptionsBuilder<MotivStoreDbContext>();
        configure(builder);
        using var context = new MotivStoreDbContext(builder.Options);

        // Act — translation happens here, without a connection; an untranslatable query throws
        var sql = EfRuleStore.HeadQuery(context).ToQueryString();

        // Assert — the superseded rows are excluded in SQL, not client-side
        sql.ShouldContain("NOT EXISTS", customMessage: provider);
        sql.ShouldContain("MotivRuleVersion", customMessage: provider);
    }
}
