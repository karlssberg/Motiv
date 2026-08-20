using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>Registers the EF Core authoring store's context factory.</summary>
public static class MotivStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IDbContextFactory{TContext}"/> for <see cref="MotivStoreDbContext"/>.
    /// A factory rather than a scoped context because the stores are singletons and
    /// <see cref="DbContext"/> is not thread-safe — and because a context per operation is what
    /// keeps the rule and proposition stores out of one another's transactions.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <param name="configure">Selects and configures the provider, e.g. <c>options.UseSqlite(...)</c>.</param>
    /// <returns>The container, to allow chained registration.</returns>
    public static IServiceCollection AddMotivEntityFrameworkStore(
        this IServiceCollection services, Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddDbContextFactory<MotivStoreDbContext>(configure);
        return services;
    }
}
