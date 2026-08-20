using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>
/// The production migration path: an adopter derives the context, adds columns of their own, and
/// owns the migrations for it. Documented as the headline reason the context is derivable, so it is
/// pinned here rather than asserted in prose — <see cref="IDbContextFactory{TContext}"/> is
/// invariant, so without the generic registration overload none of this compiles.
/// </summary>
public class DerivedContextTests
{
    private static readonly DateTimeOffset Epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static StoredRuleVersion Row(string name, int version, string? documentJson = "{}") =>
        new(name, version, documentJson, "alice", Epoch, null, null, "test");

    [Fact]
    public async Task Should_round_trip_a_rule_through_a_context_the_adopter_derived()
    {
        // Arrange — registered by the generic overload, exactly as the docs tell an adopter to
        var path = TempDatabasePath();
        try
        {
            var services = new ServiceCollection();
            services.AddMotivEntityFrameworkStore<AppStoreDbContext>(
                options => options.UseSqlite($"Data Source={path}"));

            await using var provider = services.BuildServiceProvider();

            // The stores take this exact closed type; an IDbContextFactory<AppStoreDbContext> is not
            // assignable to it, which is what the adapter registration exists to bridge.
            var factory = provider.GetRequiredService<IDbContextFactory<MotivStoreDbContext>>();

            await using (var bootstrap = await factory.CreateDbContextAsync(default))
            {
                bootstrap.ShouldBeOfType<AppStoreDbContext>();
                await bootstrap.Database.EnsureCreatedAsync();
            }

            var rules = new EfRuleStore(factory);
            var propositions = new EfPropositionStore(factory);

            // Act
            (await rules.AppendAsync([Row("a", 1, """{"v":1}""")], default)).IsConflict.ShouldBeFalse();
            (await rules.AppendAsync([Row("a", 2, """{"v":2}""")], default)).IsConflict.ShouldBeFalse();
            await propositions.WriteAsync(
                PropositionBatch.Save(new StoredProposition("p", "customer", "{}", 1, null)), default);

            // Assert — the whole store contract holds over the derived context
            var head = (await rules.LoadAsync(default)).ShouldHaveSingleItem();
            head.Version.ShouldBe(2);
            head.DocumentJson!.ShouldBe("""{"v":2}""");
            (await rules.HistoryAsync("a", default)).Select(row => row.Version).ShouldBe([1, 2]);
            propositions.Load().ShouldHaveSingleItem().Name.ShouldBe("p");
            (await rules.GetGenerationAsync(default)).ShouldBeGreaterThan(0);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task Should_carry_the_adopters_own_column_and_table_alongside_the_sdks()
    {
        // Arrange — the claim being tested is that an SDK migration can never conflict with an
        // adopter column, which is only meaningful if the adopter's additions actually persist
        var path = TempDatabasePath();
        try
        {
            var services = new ServiceCollection();
            services.AddMotivEntityFrameworkStore<AppStoreDbContext>(
                options => options.UseSqlite($"Data Source={path}"));

            await using var provider = services.BuildServiceProvider();

            // An adopter migrates through their own factory, which is registered too.
            var adopterFactory = provider.GetRequiredService<IDbContextFactory<AppStoreDbContext>>();
            await using (var bootstrap = await adopterFactory.CreateDbContextAsync(default))
                await bootstrap.Database.EnsureCreatedAsync();

            var factory = provider.GetRequiredService<IDbContextFactory<MotivStoreDbContext>>();
            await new EfRuleStore(factory).AppendAsync([Row("a", 1)], default);

            // Act
            await using var context = await adopterFactory.CreateDbContextAsync(default);
            context.Audits.Add(new AppAuditRow { Id = "1", Note = "adopter row" });
            await context.SaveChangesAsync();

            // Assert — the adopter's table, the adopter's column on the SDK's table, and the SDK's
            // own row all coexist
            var script = context.Database.GenerateCreateScript();
            script.ShouldContain("AppAudit");
            script.ShouldContain("TenantId");

            context.Audits.AsNoTracking().ShouldHaveSingleItem().Note.ShouldBe("adopter row");
            var stored = await context.RuleVersions.AsNoTracking().SingleAsync();
            stored.Name.ShouldBe("a");
            context.Entry(stored).Property<string?>("TenantId").CurrentValue.ShouldBeNull();
        }
        finally
        {
            Cleanup(path);
        }
    }

    private static string TempDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"motiv-derived-{Guid.NewGuid():N}.db");

    private static void Cleanup(string path)
    {
        // Pooled connections keep a handle on the file, so the delete below fails without this.
        SqliteConnection.ClearAllPools();
        if (File.Exists(path))
            File.Delete(path);
    }
}

/// <summary>An adopter's context: the SDK's three tables, plus a table and a column of its own.</summary>
public class AppStoreDbContext(DbContextOptions<AppStoreDbContext> options)
    : MotivStoreDbContext(options)
{
    public DbSet<AppAuditRow> Audits => Set<AppAuditRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppAuditRow>(entity =>
        {
            entity.ToTable("AppAudit");
            entity.HasKey(row => row.Id);
        });

        // A column the SDK knows nothing about, on the SDK's own table — nullable, so the store's
        // inserts neither know nor care that it is there.
        modelBuilder.Entity<RuleVersionRow>(entity => entity.Property<string?>("TenantId"));
    }
}

/// <summary>A row only the adopter has.</summary>
public class AppAuditRow
{
    public string Id { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}
