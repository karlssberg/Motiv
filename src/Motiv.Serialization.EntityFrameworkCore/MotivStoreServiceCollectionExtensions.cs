using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>Registers the EF Core authoring store's context factory.</summary>
public static class MotivStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IDbContextFactory{TContext}"/> for <see cref="MotivStoreDbContext"/>
    /// itself — the zero-config path, where the SDK's schema is the whole schema.
    /// </summary>
    /// <remarks>
    /// A factory rather than a scoped context because the stores are singletons and
    /// <see cref="DbContext"/> is not thread-safe — and because a context per operation is what
    /// keeps the rule and proposition stores out of one another's transactions.
    /// </remarks>
    /// <param name="services">The container.</param>
    /// <param name="configure">Selects and configures the provider, e.g. <c>options.UseSqlite(...)</c>.</param>
    /// <returns>The container, to allow chained registration.</returns>
    public static IServiceCollection AddMotivEntityFrameworkStore(
        this IServiceCollection services, Action<DbContextOptionsBuilder> configure) =>
        services.AddMotivEntityFrameworkStore<MotivStoreDbContext>(configure);

    /// <summary>
    /// Registers a context the adopter derived from <see cref="MotivStoreDbContext"/>, so that
    /// adopter columns and adopter-owned migrations live on a context the SDK never migrates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The generic parameter is the whole point, and is why this follows
    /// <c>AddIdentityCore&lt;TUser&gt;().AddEntityFrameworkStores&lt;TContext&gt;()</c> rather than
    /// merely resembling it. <see cref="EfRuleStore"/> and <see cref="EfPropositionStore"/> take
    /// <see cref="IDbContextFactory{TContext}"/> of <see cref="MotivStoreDbContext"/>, and that
    /// interface is <em>invariant</em>: a factory of a derived context is not assignable to it. So
    /// this registers the adopter's factory and an adapter over it, and the stores resolve exactly
    /// what they always did while every context they open is the adopter's own.
    /// </para>
    /// <para>
    /// Registering <see cref="MotivStoreDbContext"/> itself through here is legal and is what the
    /// non-generic overload does: no adapter is added in that case, because a factory that resolved
    /// itself to wrap itself would recurse forever.
    /// </para>
    /// </remarks>
    /// <typeparam name="TContext">The adopter's context, deriving from <see cref="MotivStoreDbContext"/>.</typeparam>
    /// <param name="services">The container.</param>
    /// <param name="configure">Selects and configures the provider, e.g. <c>options.UseSqlite(...)</c>.</param>
    /// <returns>The container, to allow chained registration.</returns>
    public static IServiceCollection AddMotivEntityFrameworkStore<TContext>(
        this IServiceCollection services, Action<DbContextOptionsBuilder> configure)
        where TContext : MotivStoreDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddDbContextFactory<TContext>(configure);

        if (typeof(TContext) != typeof(MotivStoreDbContext))
        {
            services.AddSingleton<IDbContextFactory<MotivStoreDbContext>>(provider =>
                new DerivedContextFactory<TContext>(
                    provider.GetRequiredService<IDbContextFactory<TContext>>()));
        }

        return services;
    }
}

/// <summary>
/// Bridges the invariance of <see cref="IDbContextFactory{TContext}"/>: hands the stores a
/// <see cref="MotivStoreDbContext"/> that is really the adopter's derived context.
/// </summary>
/// <remarks>
/// Both members forward, rather than letting the interface's default
/// <see cref="IDbContextFactory{TContext}.CreateDbContextAsync"/> wrap the synchronous one — an
/// adopter's factory may do real asynchronous work, and swallowing that would be a silent
/// downgrade to a blocking call.
/// </remarks>
internal sealed class DerivedContextFactory<TContext>(IDbContextFactory<TContext> inner)
    : IDbContextFactory<MotivStoreDbContext>
    where TContext : MotivStoreDbContext
{
    public MotivStoreDbContext CreateDbContext() => inner.CreateDbContext();

    public async Task<MotivStoreDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default) =>
        await inner.CreateDbContextAsync(cancellationToken);
}
