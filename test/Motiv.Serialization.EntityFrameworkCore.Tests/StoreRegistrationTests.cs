using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>
/// Registration is once-only. DI is last-wins, so a second call would leave two competing
/// <see cref="IDbContextFactory{TContext}"/> registrations for <see cref="MotivStoreDbContext"/> —
/// the stores would silently open contexts against whichever database was configured last. This is
/// the same refusal <c>AddPropositions</c> and <c>AddRuleStore</c> make, for the same reason: an
/// argument quietly ignored is worse than a refusal.
/// </summary>
public class StoreRegistrationTests
{
    // Two distinguishable databases. Nothing here builds a provider or opens a connection — the
    // guard runs while the container is still being described — so only their difference matters.
    private static readonly Action<DbContextOptionsBuilder> FirstDatabase =
        options => options.UseSqlite("Data Source=first.db");

    private static readonly Action<DbContextOptionsBuilder> SecondDatabase =
        options => options.UseSqlite("Data Source=second.db");

    [Fact]
    public void Should_refuse_a_second_call_to_the_zero_config_overload()
    {
        var services = new ServiceCollection();
        services.AddMotivEntityFrameworkStore(FirstDatabase);

        var act = () => services.AddMotivEntityFrameworkStore(SecondDatabase);

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain(nameof(MotivStoreServiceCollectionExtensions.AddMotivEntityFrameworkStore));
    }

    [Fact]
    public void Should_refuse_a_second_call_to_the_derived_overload()
    {
        var services = new ServiceCollection();
        services.AddMotivEntityFrameworkStore<AppStoreDbContext>(FirstDatabase);

        var act = () => services.AddMotivEntityFrameworkStore<AppStoreDbContext>(SecondDatabase);

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Should_refuse_the_derived_overload_after_the_zero_config_one()
    {
        // The shape the ticket names first: AddDbContextFactory<MotivStoreDbContext> already owns
        // the IDbContextFactory<MotivStoreDbContext> slot, and the adapter would silently take it.
        var services = new ServiceCollection();
        services.AddMotivEntityFrameworkStore(FirstDatabase);

        var act = () => services.AddMotivEntityFrameworkStore<AppStoreDbContext>(SecondDatabase);

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Should_refuse_the_zero_config_overload_after_the_derived_one()
    {
        // The mirror of the above, and the one a per-TContext sentinel would miss in both
        // directions: the adapter owns the slot, and AddDbContextFactory<MotivStoreDbContext>
        // would silently take it back.
        var services = new ServiceCollection();
        services.AddMotivEntityFrameworkStore<AppStoreDbContext>(FirstDatabase);

        var act = () => services.AddMotivEntityFrameworkStore(SecondDatabase);

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Should_refuse_a_second_derived_context()
    {
        // Two adopter contexts, two adapters, one slot — the losing store is never opened and
        // nothing says so.
        var services = new ServiceCollection();
        services.AddMotivEntityFrameworkStore<AppStoreDbContext>(FirstDatabase);

        var act = () => services.AddMotivEntityFrameworkStore<SecondStoreDbContext>(SecondDatabase);

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Should_leave_the_container_as_the_first_call_left_it()
    {
        // A guard that threw after AddDbContextFactory had already run would corrupt the very
        // state it exists to protect.
        var services = new ServiceCollection();
        services.AddMotivEntityFrameworkStore<AppStoreDbContext>(FirstDatabase);
        var registeredByTheFirstCall = services.Count;

        var act = () => services.AddMotivEntityFrameworkStore<SecondStoreDbContext>(SecondDatabase);

        act.ShouldThrow<InvalidOperationException>();
        services.Count.ShouldBe(registeredByTheFirstCall);
        services.ShouldNotContain(descriptor =>
            descriptor.ServiceType == typeof(IDbContextFactory<SecondStoreDbContext>));
    }

    [Fact]
    public void Should_still_allow_the_first_call()
    {
        var services = new ServiceCollection();

        services.AddMotivEntityFrameworkStore<AppStoreDbContext>(FirstDatabase);

        services.ShouldContain(descriptor =>
            descriptor.ServiceType == typeof(IDbContextFactory<MotivStoreDbContext>));
    }

    [Fact]
    public void Should_reject_a_null_argument_before_consulting_the_guard()
    {
        // Argument validation stays ahead of the once-only check, so a null configure on a first
        // call is still an ArgumentNullException rather than a confusing "already called".
        var services = new ServiceCollection();

        var act = () => services.AddMotivEntityFrameworkStore<AppStoreDbContext>(configure: null!);

        act.ShouldThrow<ArgumentNullException>();
    }
}

/// <summary>A second adopter context, so the two-derived-contexts collision has something to collide with.</summary>
public class SecondStoreDbContext(DbContextOptions<SecondStoreDbContext> options)
    : MotivStoreDbContext(options);
