using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

    /// <summary>
    /// Enables runtime-authored propositions, backed by the given store (in-memory when omitted).
    /// The <see cref="PropositionSet"/> shares the <see cref="RuleSet"/>'s coordinator, so a
    /// proposition edit and a rule update can never interleave.
    /// </summary>
    /// <param name="store">Where authored propositions persist, or null for in-memory.</param>
    /// <returns>This builder, to allow chained registration.</returns>
    /// <exception cref="InvalidOperationException">Propositions are already enabled. DI is
    /// last-wins, so a second call would silently discard the first store rather than layering
    /// onto it — an argument quietly ignored is worse than a refusal.</exception>
    public MotivRulesBuilder AddPropositions(IPropositionStore? store = null)
    {
        if (Services.Any(descriptor => descriptor.ServiceType == typeof(PropositionSet)))
            throw new InvalidOperationException(
                $"{nameof(AddPropositions)} has already been called. Call it once — a second call " +
                "would silently replace the first store, as DI registration is last-wins.");

        Services.AddSingleton<IPropositionStore>(store ?? new InMemoryPropositionStore());
        Services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<MotivRulesOptions>();
            var propositions = new PropositionSet(
                provider.GetRequiredService<BindingScope>(),
                provider.GetRequiredService<IPropositionStore>(),
                options.SerializerOptions);

            foreach (var register in options.PropositionModelRegistrations)
                register(propositions);

            propositions.Load();
            return propositions;
        });
        return this;
    }

    /// <summary>
    /// Enables the governance workflow: an <see cref="ApprovalGate"/> and a
    /// <see cref="ChangeRequestSet"/> over the <see cref="RuleSet"/> and, when
    /// <see cref="AddPropositions"/> was also called, the <see cref="PropositionSet"/>. Mounting the
    /// endpoints then adds the <c>change-requests</c> routes and — the point of the whole exercise —
    /// routes every direct write through the same gate, so the ungoverned surface cannot be used to
    /// walk around the ceremony.
    /// </summary>
    /// <remarks>
    /// The gate's default is permissive, so enabling governance changes no response until a gate
    /// document is installed. Both singletons are built from factories, so this may be called before
    /// or after <see cref="AddPropositions"/> — the set that exists when the workflow is first
    /// resolved is the one it governs.
    /// </remarks>
    /// <param name="gateStore">Where the active gate document persists, or null to run without persistence.</param>
    /// <returns>This builder, to allow chained registration.</returns>
    /// <exception cref="InvalidOperationException">Governance is already enabled. DI is last-wins,
    /// so a second call would silently discard the first store rather than layering onto it.</exception>
    public MotivRulesBuilder AddGovernance(IGateStore? gateStore = null)
    {
        if (Services.Any(descriptor => descriptor.ServiceType == typeof(ChangeRequestSet)))
            throw new InvalidOperationException(
                $"{nameof(AddGovernance)} has already been called. Call it once — a second call " +
                "would silently replace the first gate store, as DI registration is last-wins.");

        Services.AddSingleton(_ => new ApprovalGate(gateStore));
        Services.AddSingleton(provider => new ChangeRequestSet(
            provider.GetRequiredService<ApprovalGate>(),
            provider.GetRequiredService<RuleSet>(),
            provider.GetService<PropositionSet>()));

        // TryAdd, not Add: a host that wants break-glass registers its own BreakGlass *after*
        // AddGovernance (AddSingleton overrides TryAdd), so this only fills the slot when nobody
        // else has. The default is off, so enabling governance changes no publish behaviour until
        // a host deliberately opts in.
        Services.TryAddSingleton(BreakGlass.Off);
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
        services.AddSingleton(provider => new BindingScope(provider.GetRequiredService<SpecRegistry>()));
        services.AddSingleton(provider =>
        {
            // Resolve from the provider rather than closing over the parameters, so the
            // RuleSet always shares whatever registry/options the endpoints resolve —
            // even if a later registration shadowed the ones passed here.
            var resolvedOptions = provider.GetRequiredService<MotivRulesOptions>();

            // Propositions load first: a rule's *default* document may reference an authored
            // proposition, and Add binds that default immediately. Resolved for that side effect
            // alone — the RuleSet reaches the same propositions through the shared BindingScope.
            _ = provider.GetService<PropositionSet>();

            var rules = new RuleSet(
                provider.GetRequiredService<BindingScope>(),
                resolvedOptions.SerializerOptions);
            foreach (var rule in provider.GetServices<RuleBase>())
                rules.Add(rule);
            return rules;
        });
        return new MotivRulesBuilder(services);
    }
}
