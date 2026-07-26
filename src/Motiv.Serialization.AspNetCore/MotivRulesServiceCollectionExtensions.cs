using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore;

/// <summary>A builder for enrolling rules after <see cref="MotivRulesServiceCollectionExtensions.AddMotivRules"/>.</summary>
public sealed class MotivRulesBuilder
{
    internal MotivRulesBuilder(IServiceCollection services) => Services = services;

    /// <summary>The underlying service collection, for advanced scenarios.</summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Registers a rule as a singleton under its concrete type and enrolls it in the
    /// <see cref="RuleSet"/>. Inject the concrete type wherever the rule is executed. The handle
    /// is only bound once the <see cref="RuleSet"/> is resolved (e.g. by
    /// <c>MapMotivRules(basePath)</c>) — evaluating it before then throws.
    /// </summary>
    /// <typeparam name="TRule">The sealed rule class (parameterless constructor).</typeparam>
    /// <returns>This builder, to allow chained registration.</returns>
    public MotivRulesBuilder AddRule<TRule>() where TRule : RuleBase, new()
    {
        Services.AddSingleton<TRule>(static _ => new TRule());
        Services.AddSingleton<RuleBase>(provider => provider.GetRequiredService<TRule>());
        return this;
    }

    /// <summary>
    /// Registers an existing rule instance and enrolls it in the <see cref="RuleSet"/>. The
    /// instance is only bound once the <see cref="RuleSet"/> is resolved (e.g. by
    /// <c>MapMotivRules(basePath)</c>) — evaluating it before then throws.
    /// </summary>
    /// <typeparam name="TRule">The rule's concrete type.</typeparam>
    /// <param name="rule">The rule instance.</param>
    /// <returns>This builder, to allow chained registration.</returns>
    public MotivRulesBuilder AddRule<TRule>(TRule rule) where TRule : RuleBase
    {
        // Register the concrete slot by runtime type — when TRule infers as RuleBase
        // (e.g. enrolling from a RuleBase-typed variable), AddSingleton(rule) would
        // occupy the RuleBase slot and enroll the rule twice.
        Services.AddSingleton(rule.GetType(), rule);
        Services.AddSingleton<RuleBase>(rule);
        return this;
    }
}

/// <summary>DI registration for the Motiv rules endpoints and live rules.</summary>
public static class MotivRulesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the registry, options, and a <see cref="RuleSet"/> singleton built from every
    /// rule enrolled via <see cref="MotivRulesBuilder.AddRule{TRule}()"/>. The RuleSet is
    /// constructed with this same registry and <see cref="MotivRulesOptions.SerializerOptions"/>
    /// that the DI <c>MapMotivRules(basePath)</c> overload later maps the endpoints with, so the
    /// validate/evaluate endpoints and the rule-update endpoints can never disagree on how
    /// documents bind. The RuleSet binds all rule defaults when first resolved —
    /// <c>MapMotivRules(basePath)</c> resolves it eagerly so invalid defaults fail at startup.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="registry">The registry rule documents resolve spec references against.</param>
    /// <param name="options">The endpoint options, including evaluable model registrations.</param>
    /// <returns>A builder for enrolling rules.</returns>
    public static MotivRulesBuilder AddMotivRules(
        this IServiceCollection services,
        SpecRegistry registry,
        MotivRulesOptions options)
    {
        services.AddSingleton(registry);
        services.AddSingleton(options);
        services.AddSingleton(provider =>
        {
            // Resolve from the provider rather than closing over the parameters, so the
            // RuleSet always shares whatever registry/options the endpoints resolve —
            // even if a later registration shadowed the ones passed here.
            var resolvedOptions = provider.GetRequiredService<MotivRulesOptions>();
            var rules = new RuleSet(
                provider.GetRequiredService<SpecRegistry>(),
                resolvedOptions.SerializerOptions);
            foreach (var rule in provider.GetServices<RuleBase>())
                rules.Add(rule);
            return rules;
        });
        return new MotivRulesBuilder(services);
    }
}
