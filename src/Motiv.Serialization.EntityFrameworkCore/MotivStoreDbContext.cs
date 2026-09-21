using Microsoft.EntityFrameworkCore;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>
/// The authoring store's schema: one consistent unit and one backup unit, holding rules,
/// propositions and where each store stands.
/// </summary>
/// <remarks>
/// <para>
/// Derivable, following <c>Microsoft.AspNetCore.Identity.EntityFrameworkCore</c>: an adopter derives
/// this to add their own columns and owns their migrations, so an SDK migration can never conflict
/// with an adopter column.
/// </para>
/// <para>
/// The constraints here are identity and structure only — primary keys and NOT NULL. Nothing encodes
/// binding legality, which <c>RuleSet</c> decides and quarantine-on-load revalidates. The
/// <c>(Name, Version)</c> key on <see cref="RuleVersions"/> is the exception that proves the rule: it
/// is structural, and it is the cross-process compare-and-set.
/// </para>
/// </remarks>
public class MotivStoreDbContext : DbContext
{
    /// <summary>For the common case: a context configured for itself.</summary>
    public MotivStoreDbContext(DbContextOptions<MotivStoreDbContext> options) : base(options)
    {
    }

    /// <summary>For a derived context, which passes its own options type.</summary>
    protected MotivStoreDbContext(DbContextOptions options) : base(options)
    {
    }

    /// <summary>The append-only rule version log.</summary>
    public DbSet<RuleVersionRow> RuleVersions => Set<RuleVersionRow>();

    /// <summary>The proposition version log: one row per <c>(Name, Version)</c>, tombstones included.</summary>
    public DbSet<PropositionVersionRow> PropositionVersions => Set<PropositionVersionRow>();

    /// <summary>Where each store stands, one row per scope.</summary>
    public DbSet<StoreGenerationRow> StoreGenerations => Set<StoreGenerationRow>();

    /// <summary>Each rule's scenarios: one row per <c>(RuleName, Id)</c>, replaced in place.</summary>
    public DbSet<ScenarioRow> Scenarios => Set<ScenarioRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RuleVersionRow>(entity =>
        {
            entity.ToTable("MotivRuleVersion");
            entity.HasKey(row => new { row.Name, row.Version });
            entity.Property(row => row.Name).IsRequired();
            entity.Property(row => row.Author).IsRequired();
            // Portable text, not a native jsonb/json column: the sink never queries into the
            // document, so native JSON buys nothing and would fork the schema per provider.
            entity.Property(row => row.DocumentJson);
        });

        modelBuilder.Entity<PropositionVersionRow>(entity =>
        {
            entity.ToTable("MotivPropositionVersion");
            // The composite key is the cross-process compare-and-set, exactly as for rules: two
            // replicas racing the same version race on the insert, and the key lets exactly one win.
            entity.HasKey(row => new { row.Name, row.Version });
            entity.Property(row => row.Name).IsRequired();
            entity.Property(row => row.Author).IsRequired();
            entity.Property(row => row.DocumentJson);
        });

        modelBuilder.Entity<ScenarioRow>(entity =>
        {
            entity.ToTable("MotivScenario");
            entity.HasKey(row => new { row.RuleName, row.Id });
            entity.Property(row => row.Name).IsRequired();
            entity.Property(row => row.ModelJson).IsRequired();
            entity.Property(row => row.Author).IsRequired();
            // Rows are replaced in place, so the version is a concurrency token: every UPDATE and
            // DELETE carries `AND Version = @original`, and a replica that committed first leaves
            // this one matching no rows — DbUpdateConcurrencyException, provider-agnostic.
            entity.Property(row => row.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<StoreGenerationRow>(entity =>
        {
            entity.ToTable("MotivStoreGeneration");
            entity.HasKey(row => row.Scope);
        });
    }
}
