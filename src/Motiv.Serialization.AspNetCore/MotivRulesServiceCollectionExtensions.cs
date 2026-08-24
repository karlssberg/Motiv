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
    /// Registers the decision log every rule marked <c>audited</c> records to, writing through
    /// <paramref name="sink"/>. Without this, an audited rule document will not bind — which is the
    /// intended fail-closed behaviour, not an oversight.
    /// </summary>
    /// <remarks>
    /// <paramref name="configure"/> <strong>must</strong> register a capture posture for every model
    /// type an audited rule evaluates (<see cref="DecisionLogOptions.Capture"/>). There is no default:
    /// what a decision record may keep of a model is the adopter's call, and a whole-model default
    /// that applied by omission would store whatever personal data the model happens to hold.
    /// </remarks>
    /// <param name="sink">Where records are written.</param>
    /// <param name="configure">Queue size, backpressure posture, and — required — the capture postures.</param>
    /// <returns>This builder, to allow chained registration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is null.</exception>
    public MotivRulesBuilder AddDecisionLog(IDecisionSink sink, Action<DecisionLogOptions>? configure = null)
    {
        if (sink is null) throw new ArgumentNullException(nameof(sink));
        return AddDecisionLog(_ => sink, configure);
    }

    /// <summary>
    /// Registers the decision log with a sink resolved from the service provider — for a durable sink
    /// that needs a connection, a context factory, or a client of its own.
    /// </summary>
    /// <param name="sinkFactory">Builds the sink.</param>
    /// <param name="configure">Queue size, backpressure posture, and — required — the capture postures.</param>
    /// <returns>This builder, to allow chained registration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sinkFactory"/> is null.</exception>
    public MotivRulesBuilder AddDecisionLog(
        Func<IServiceProvider, IDecisionSink> sinkFactory, Action<DecisionLogOptions>? configure = null)
    {
        if (sinkFactory is null) throw new ArgumentNullException(nameof(sinkFactory));

        // A singleton, so the container disposes it at shutdown -- which is what drains the queue.
        // A decision log that were scoped would take its unwritten records down with each request.
        Services.AddSingleton(provider =>
        {
            var logOptions = new DecisionLogOptions();
            configure?.Invoke(logOptions);
            return new DecisionLog(sinkFactory(provider), logOptions);
        });

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
    public MotivRulesBuilder AddPropositions(IPropositionStore? store = null) =>
        AddPropositionsCore(_ => store ?? new InMemoryPropositionStore());

    /// <summary>
    /// Enables runtime-authored propositions, backed by a store built from the container. For a
    /// store with dependencies of its own — a database context factory, for instance — which
    /// therefore cannot be constructed before the container exists.
    /// </summary>
    /// <param name="storeFactory">Builds the store once the container is available.</param>
    /// <returns>This builder, to allow chained registration.</returns>
    /// <exception cref="InvalidOperationException">Propositions are already enabled.</exception>
    public MotivRulesBuilder AddPropositions(Func<IServiceProvider, IPropositionStore> storeFactory)
    {
        ArgumentNullException.ThrowIfNull(storeFactory);
        return AddPropositionsCore(storeFactory);
    }

    private MotivRulesBuilder AddPropositionsCore(Func<IServiceProvider, IPropositionStore> storeFactory)
    {
        if (Services.Any(descriptor => descriptor.ServiceType == typeof(PropositionSet)))
            throw new InvalidOperationException(
                $"{nameof(AddPropositions)} has already been called. Call it once — a second call " +
                "would silently replace the first store, as DI registration is last-wins.");

        Services.AddSingleton<IPropositionStore>(storeFactory);
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

    /// <summary>
    /// Registers where rules persist, and loads them when the <see cref="RuleSet"/> is first resolved.
    /// Without this, rules live for the lifetime of the process, as they always have.
    /// </summary>
    /// <param name="store">The store, or null for <see cref="InMemoryRuleStore"/>.</param>
    /// <param name="failFastOnQuarantine">
    /// Whether a stored document that no longer binds should stop startup. Defaults to <c>true</c>:
    /// a quarantined rule is running its compiled default, which is <em>not what was published</em>,
    /// and under an approval gate booting quietly into unapproved behaviour is the worse failure. Set
    /// false to boot anyway and read the quarantine from the catalog.
    /// </param>
    /// <returns>This builder, to allow chained registration.</returns>
    /// <exception cref="InvalidOperationException">A rule store is already registered. DI is
    /// last-wins, so a second call would silently discard the first store rather than layering onto
    /// it — an argument quietly ignored is worse than a refusal.</exception>
    public MotivRulesBuilder AddRuleStore(IRuleStore? store = null, bool failFastOnQuarantine = true) =>
        AddRuleStoreCore(_ => store ?? new InMemoryRuleStore(), failFastOnQuarantine);

    /// <summary>
    /// Points the live rules at a store built from the container. For a store with dependencies of
    /// its own, which therefore cannot be constructed before the container exists.
    /// </summary>
    /// <param name="storeFactory">Builds the store once the container is available.</param>
    /// <param name="failFastOnQuarantine">As the instance overload.</param>
    /// <returns>This builder, to allow chained registration.</returns>
    /// <exception cref="InvalidOperationException">A rule store is already registered.</exception>
    public MotivRulesBuilder AddRuleStore(
        Func<IServiceProvider, IRuleStore> storeFactory, bool failFastOnQuarantine = true)
    {
        ArgumentNullException.ThrowIfNull(storeFactory);
        return AddRuleStoreCore(storeFactory, failFastOnQuarantine);
    }

    /// <summary>
    /// Points the live rules at a store so a hot-swapped rule survives a restart instead of
    /// reverting to its compiled default.
    /// </summary>
    private MotivRulesBuilder AddRuleStoreCore(
        Func<IServiceProvider, IRuleStore> storeFactory, bool failFastOnQuarantine)
    {
        if (Services.Any(descriptor => descriptor.ServiceType == typeof(RuleStoreOptions)))
            throw new InvalidOperationException(
                $"{nameof(AddRuleStore)} has already been called. Call it once — a second call " +
                "would silently replace the first store, as DI registration is last-wins.");

        // Registered under the interface explicitly, as AddPropositions does for its own store: the
        // RuleSet factory resolves IRuleStore, and leaving the service type to type inference makes
        // that dependence on the parameter's declared type invisible.
        Services.AddSingleton<IRuleStore>(storeFactory);
        Services.AddSingleton(new RuleStoreOptions(failFastOnQuarantine));
        return this;
    }

    /// <summary>
    /// Polls the stores and rebuilds this replica when another one publishes. Opt-in, because a
    /// single-replica host does not need it.
    /// </summary>
    /// <param name="interval">How often to poll, or null for the five-second default.</param>
    /// <returns>This builder, to allow chained registration.</returns>
    /// <exception cref="InvalidOperationException">Refresh is already enabled. DI is last-wins, so a
    /// second call would silently discard the first interval and, worse, register a second
    /// <see cref="IHostedService"/> slot resolving the same <see cref="MotivRefreshService"/>
    /// singleton — two concurrent poll loops against one store and one <c>LastReport</c> — rather
    /// than layering onto the first call.</exception>
    public MotivRulesBuilder AddRefresh(TimeSpan? interval = null)
    {
        if (Services.Any(descriptor => descriptor.ServiceType == typeof(MotivRefreshOptions)))
            throw new InvalidOperationException(
                $"{nameof(AddRefresh)} has already been called. Call it once — a second call " +
                "would start a second poll loop against the same MotivRefreshService singleton, " +
                "as DI registration is last-wins.");

        var options = new MotivRefreshOptions();
        if (interval is { } value)
            options.Interval = value;

        Services.AddSingleton(options);
        Services.AddSingleton<MotivRefreshService>();
        Services.AddHostedService(provider => provider.GetRequiredService<MotivRefreshService>());

        // AddCheck<MotivRefreshHealthCheck>, not an instance factory: the DI-resolved constructor
        // pulls the MotivRefreshService singleton above, so the check reads the exact instance the
        // poll loop writes to, the same way the hosted service registration does.
        Services.AddHealthChecks().AddCheck<MotivRefreshHealthCheck>("motiv-refresh");
        return this;
    }
}

/// <summary>
/// Whether <see cref="MotivRulesBuilder.AddRuleStore"/> should stop startup on quarantine. Public so a
/// host that registers an <see cref="IRuleStore"/> directly on <see cref="IServiceCollection"/> —
/// documented on <see cref="MotivRulesServiceCollectionExtensions.AddMotivRules"/> as a supported
/// escape hatch from <see cref="MotivRulesBuilder.AddRuleStore"/> — can still register this and have
/// its choice honoured, rather than the escape hatch silently and permanently fail-fasting because the
/// type that carries the setting was unreachable outside this assembly.
/// </summary>
/// <param name="FailFastOnQuarantine">
/// See <see cref="MotivRulesBuilder.AddRuleStore"/>'s parameter of the same name.
/// </param>
public sealed record RuleStoreOptions(bool FailFastOnQuarantine);

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

            // Resolved once: the same store instance both backs the RuleSet and decides, by its
            // presence, whether there is anything to load below.
            var store = provider.GetService<IRuleStore>();

            var rules = new RuleSet(
                provider.GetRequiredService<BindingScope>(),
                store,
                options: resolvedOptions.SerializerOptions,
                // Optional: with no log registered, an audited document simply does not bind, and
                // says so. Resolved here rather than closed over so AddDecisionLog can be called
                // either side of AddMotivRules.
                decisionLog: provider.GetService<DecisionLog>());
            foreach (var rule in provider.GetServices<RuleBase>())
                rules.Add(rule);

            // Load after every rule is registered — a stored head can only apply to a rule that
            // exists — and after the PropositionSet has loaded, since a stored rule document may
            // reference an authored proposition.
            if (store is not null)
            {
                var report = rules.Load();

                // RuleStoreOptions is only absent when a host registered IRuleStore directly on
                // Services instead of going through AddRuleStore — a supported escape hatch, not a
                // wiring bug, so this tolerates the gap rather than GetRequiredService's opaque "no
                // service for type" and degrades to AddRuleStore's own documented default.
                var failFast = provider.GetService<RuleStoreOptions>()?.FailFastOnQuarantine ?? true;
                if (failFast)
                    report.ThrowIfQuarantined();
            }

            return rules;
        });
        return new MotivRulesBuilder(services);
    }
}
