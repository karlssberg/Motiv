using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore;

/// <summary>Extension methods that mount the Motiv rules endpoints on an ASP.NET Core app.</summary>
public static class MotivRulesEndpoints
{
    /// <summary>
    /// The response header carrying the world a response was served from — the wire form of a
    /// <see cref="StoreGeneration"/>, as produced by <see cref="StoreGeneration.ToToken"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A client polling several replicas behind a load balancer has no other way to tell it was
    /// routed to one that has not caught up: the response itself looks perfectly well-formed, and
    /// nothing about HTTP distinguishes a fresh answer from a stale one. Comparing the last generation
    /// it saw against this header is how it finds out — the difference between eventual consistency,
    /// which a client can reason about, and silent divergence, which it cannot. A public constant so
    /// the header name is spelled once and shared, not duplicated, between this project's tests and
    /// any client (including the TypeScript client) that needs to parse it with
    /// <see cref="StoreGeneration.TryParseToken"/>.
    /// </para>
    /// <para>
    /// <strong>Present on every response the Motiv endpoints themselves produce — not on a refusal
    /// issued above them.</strong> <c>MotivGenerationFilter</c> stamps it, and a filter only runs once
    /// routing has selected a Motiv endpoint and cleared whatever ASP.NET middleware sits in front of
    /// it. An unauthenticated request refused by <c>RequireAuthorization()</c> short-circuits before
    /// the filter ever runs, so its 401 carries no header — which is the right outcome, not a gap: a
    /// caller refused for lacking credentials has no generation to compare against anyway. A document
    /// this endpoint itself rejects (a 404 unknown rule, a 400 invalid document) is different — that
    /// refusal is produced *inside* the pipeline the filter wraps, so it is stamped like any other
    /// response.
    /// </para>
    /// <para>
    /// <strong>Names the world the request was pinned to, which is the world the catalog and the rule
    /// and proposition listings are read from — and, for a write, the world <em>before</em> the
    /// write.</strong> The pin is taken at request start, before a <c>PUT</c>'s publish commits, so a
    /// successful write's header reports the pre-write generation while its body reports the
    /// post-write version — two true facts about two different moments, not a bug.
    /// <c>POST /validate</c> and <c>POST /evaluate</c> are a second, milder exception in the same
    /// direction: they bind an ad-hoc document, and binding reads the <em>live</em> world by the
    /// settled rule that anything which binds or publishes does — so their bodies may reflect a world
    /// at or ahead of the one the header names, never behind it. That direction is the safe one: this
    /// token exists so a client can detect being routed *backwards*, and a client tracks the
    /// generation it last accepted as served to it. Understating what a response
    /// carries can only make a client miss a genuine improvement it just received — a false negative
    /// on skew detection. Overstating would have it record a generation it was never actually served,
    /// so the very next correct response would look like a regression and raise a false alarm. The one
    /// real consequence worth naming: a writer cannot use its own write's response header to tell
    /// whether a later read from the same connection is stale — that comparison needs the header from
    /// the later read, not this one.
    /// </para>
    /// </remarks>
    public const string GenerationHeader = "Motiv-Generation";

    /// <summary>
    /// Maps <c>GET {basePath}/catalog</c>, <c>POST {basePath}/validate</c>, and
    /// <c>POST {basePath}/evaluate</c>, backed by the given registry and options. When a
    /// <see cref="RuleSet"/> is supplied, also maps <c>GET {basePath}/rules</c>,
    /// <c>GET {basePath}/rules/{{name}}</c>, <c>PUT {basePath}/rules/{{name}}</c>, and
    /// <c>DELETE {basePath}/rules/{{name}}</c> for live rule management with optimistic concurrency.
    /// When a <see cref="PropositionSet"/> is resolvable from the endpoint route builder's service
    /// provider (i.e. <see cref="MotivRulesBuilder.AddPropositions"/> was called), the
    /// <c>{basePath}/propositions</c> endpoints are mapped against it as well, and documents bind
    /// through its authored layer over the registry, so validate/evaluate resolve the same names the
    /// catalog lists. This overload cannot substitute a different one, so pass the same registry and
    /// options it was built with. The mapped group is secure by default — callers must be
    /// authenticated — unless <paramref name="configureEndpoints"/> calls
    /// <see cref="MotivRulesEndpointOptions.AllowAnonymous"/>, the explicit, greppable escape.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to map onto.</param>
    /// <param name="basePath">The base path to mount under, e.g. <c>/api/rules</c>.</param>
    /// <param name="registry">The registry of specs documents may reference.</param>
    /// <param name="options">The endpoint options, including evaluable model registrations.</param>
    /// <param name="rules">The live rule set to manage, or null to omit the rule endpoints.
    /// Construct it with the same registry and <see cref="MotivRulesOptions.SerializerOptions"/>
    /// passed here, so validate/evaluate and rule updates agree on how documents bind.</param>
    /// <param name="configureEndpoints">Configures per-mount endpoint behavior. The mapped group
    /// is secure by default (<c>RequireAuthorization</c>); call
    /// <see cref="MotivRulesEndpointOptions.AllowAnonymous"/> here to open it to unauthenticated
    /// callers — an explicit, greppable escape rather than a silent default.</param>
    /// <returns>The endpoint route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapMotivRules(
        this IEndpointRouteBuilder endpoints,
        string basePath,
        SpecRegistry registry,
        MotivRulesOptions options,
        RuleSet? rules = null,
        Action<MotivRulesEndpointOptions>? configureEndpoints = null)
    {
        var propositions = endpoints.ServiceProvider.GetService<PropositionSet>();

        // The catalog lists authored propositions and RuleSet binds documents referencing them,
        // both through the scope's layered source. Binding over the bare registry here would leave
        // validate/evaluate rejecting, as UnknownSpec, the very names the rest of the surface
        // advertises and accepts.
        var specSource = propositions?.Scope.Source ?? registry;
        var serializer = new RuleSerializer(specSource, options.SerializerOptions);
        var resultSerializer = new ResultSerializer();
        var json = options.JsonSerializerOptions;
        var group = endpoints.MapGroup(basePath);

        var endpointOptions = new MotivRulesEndpointOptions();
        configureEndpoints?.Invoke(endpointOptions);
        if (endpointOptions.Anonymous)
            group.AllowAnonymous();
        else
            group.RequireAuthorization();

        // Either set pins the same scope when they share one; a registry-only mount has no scope to
        // pin and no generation to report, so it is left alone.
        Func<string?, string?, DecisionSnapshot>? pin = null;
        if (rules is not null)
            pin = (correlationId, caller) => rules.PinSnapshot(correlationId, caller);
        else if (propositions is not null)
            pin = (correlationId, caller) => propositions.PinSnapshot(correlationId, caller);

        if (pin is not null)
            group.AddEndpointFilter(new MotivGenerationFilter(pin));

        MapCatalogEndpoint(group, registry, options, rules, propositions, json);

        group.MapPost("/validate", (ValidateRequest request, HttpContext http) =>
        {
            if (GrantGate.RefuseUnlessAuthorAnywhere(http, json) is { } refusal)
                return refusal;

            if (request.Document.ValueKind == JsonValueKind.Undefined)
                return EndpointResponses.MissingDocument(json);

            if (!options.TryGetBinding(request.ModelType, out var binding))
                return UnknownModelType(request.ModelType, json);

            var documentJson = request.Document.GetRawText();
            var errors = request.IsAsync
                ? binding.ValidateAsyncSpec(serializer, documentJson)
                : binding.Validate(serializer, documentJson);
            return Results.Json(new ValidationResponse(errors), json);
        });

        group.MapPost("/evaluate", (EvaluateRequest request, HttpContext http) =>
        {
            if (GrantGate.RefuseUnlessAuthorAnywhere(http, json) is { } refusal)
                return refusal;

            if (request.Document.ValueKind == JsonValueKind.Undefined)
                return EndpointResponses.MissingDocument(json);

            if (request.Model.ValueKind == JsonValueKind.Undefined)
                return Results.Json(
                    new ErrorResponse("The request must include a model."), json, statusCode: 400);

            if (!options.TryGetBinding(request.ModelType, out var binding))
                return UnknownModelType(request.ModelType, json);

            try
            {
                var result = binding.Evaluate(
                    serializer, resultSerializer, json, request.Document.GetRawText(), request.Model);
                return Results.Json(result, json);
            }
            catch (RuleSerializationException ex)
            {
                return Results.Json(new ValidationResponse(ex.Errors), json, statusCode: 400);
            }
            catch (InvalidModelException ex)
            {
                return Results.Json(new ErrorResponse(ex.Message), json, statusCode: 400);
            }
        });

        var governance = ResolveGovernance(endpoints, rules);
        if (governance is not null)
        {
            MotivGovernanceEndpoints.MapChangeRequestEndpoints(group, governance, json);

            // AddGovernance() registers the ApprovalGate and the ChangeRequestSet together, so one
            // resolving means the other always does too.
            var gate = endpoints.ServiceProvider.GetRequiredService<ApprovalGate>();
            MotivGovernanceEndpoints.MapGateEndpoints(group, gate, json);
        }

        if (rules is not null)
            MapRuleEndpoints(group, rules, governance, options, json);

        if (propositions is not null)
            MotivPropositionEndpoints.MapPropositionEndpoints(group, propositions, governance, json);

        return endpoints;
    }

    /// <summary>
    /// The registered governance workflow, or null when <see cref="MotivRulesBuilder.AddGovernance"/>
    /// was never called.
    /// </summary>
    /// <remarks>
    /// A governance set publishes into the <see cref="RuleSet"/> it was constructed with, which is
    /// the one in DI. Mounting a *different* RuleSet alongside it would leave the endpoints reading
    /// one set while governed writes published to another — a silent, security-relevant divergence,
    /// so it is refused at startup rather than discovered in production.
    /// </remarks>
    private static ChangeRequestSet? ResolveGovernance(IEndpointRouteBuilder endpoints, RuleSet? rules)
    {
        if (endpoints.ServiceProvider.GetService<ChangeRequestSet>() is not { } governance)
            return null;

        if (rules is not null
            && endpoints.ServiceProvider.GetService<RuleSet>() is { } registered
            && !ReferenceEquals(registered, rules))
        {
            throw new InvalidOperationException(
                "AddGovernance() registered a ChangeRequestSet over the RuleSet in the service " +
                "provider, but MapMotivRules was handed a different RuleSet instance. Governed " +
                "writes would publish into the registered set while these endpoints read the one " +
                "passed here. To fix: mount with the MapMotivRules(basePath) overload, which uses " +
                "the registered RuleSet; or pass that same RuleSet " +
                "(services.GetRequiredService<RuleSet>()) to this overload; or drop the " +
                "AddGovernance() call if this mount is not meant to be governed.");
        }

        return governance;
    }

    /// <summary>
    /// Maps the rules endpoints using the registry, options, and <see cref="RuleSet"/> registered
    /// via <see cref="MotivRulesServiceCollectionExtensions.AddMotivRules"/> — the RuleSet is
    /// guaranteed to share that registry and serializer options, so validate/evaluate and rule
    /// updates cannot diverge. Resolves the RuleSet eagerly so an invalid rule default fails
    /// here, at startup, rather than at first request. The mapped group is secure by default —
    /// callers must be authenticated — unless <paramref name="configureEndpoints"/> calls
    /// <see cref="MotivRulesEndpointOptions.AllowAnonymous"/>, the explicit, greppable escape.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to map onto.</param>
    /// <param name="basePath">The base path to mount under, e.g. <c>/api/rules</c>.</param>
    /// <param name="configureEndpoints">Configures per-mount endpoint behavior. The mapped group
    /// is secure by default (<c>RequireAuthorization</c>); call
    /// <see cref="MotivRulesEndpointOptions.AllowAnonymous"/> here to open it to unauthenticated
    /// callers — an explicit, greppable escape rather than a silent default.</param>
    /// <returns>The endpoint route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapMotivRules(
        this IEndpointRouteBuilder endpoints,
        string basePath,
        Action<MotivRulesEndpointOptions>? configureEndpoints = null)
    {
        var services = endpoints.ServiceProvider;
        if (services.GetService<SpecRegistry>() is not { } registry
            || services.GetService<MotivRulesOptions>() is not { } options)
        {
            throw new InvalidOperationException(
                "The Motiv rules services are not registered. " +
                "Call services.AddMotivRules(registry, options) before MapMotivRules(basePath).");
        }

        // Resolving the RuleSet binds every enrolled rule's default — an invalid default
        // fails here, at startup, rather than at first request.
        return endpoints.MapMotivRules(
            basePath, registry, options, services.GetRequiredService<RuleSet>(), configureEndpoints);
    }

    /// <summary>
    /// Maps <c>GET /catalog</c>. The collection, metadata and model listings are fixed at startup,
    /// but the spec listing is rebuilt per request: authoring a proposition changes the effective
    /// spec list, and a constant catalog would hide every new proposition until restart.
    /// </summary>
    private static void MapCatalogEndpoint(
        RouteGroupBuilder group,
        SpecRegistry registry,
        MotivRulesOptions options,
        RuleSet? rules,
        PropositionSet? propositions,
        JsonSerializerOptions json)
    {
        var collections = registry.Collections
            .Select(collection => new CatalogCollection(
                collection.Path,
                options.ResolveModelId(collection.ParentType),
                options.ResolveModelId(collection.ElementType)))
            .ToArray();

        // Each schema is generated with the options its type is actually deserialized with —
        // metadata payloads bind with the metadata options, models with the response options —
        // so schema property names match real binding behavior by construction.
        var metadataJson = options.SerializerOptions?.MetadataJsonOptions ?? JsonSerializerOptions.Default;

        var metadataTypes = registry.Entries.Select(entry => entry.MetadataType)
            .Concat(rules?.Rules.Select(rule => rule.MetadataType) ?? [])
            .Distinct()
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToDictionary(type => type.Name, type => ToSchema(metadataJson, type));

        var modelTypes = options.ModelBindings
            .OrderBy(binding => binding.Id, StringComparer.Ordinal)
            .ToDictionary(binding => binding.Id, binding => ToSchema(json, binding.ModelType));

        group.MapGet("/catalog", () => Results.Json(
            new CatalogResponse(
                propositions is null ? CompiledSpecs(registry, options) : EffectiveSpecs(propositions, registry),
                collections,
                metadataTypes,
                modelTypes),
            json));
    }

    /// <summary>The compiled registry as catalog entries — the listing when propositions are not enabled.</summary>
    private static IReadOnlyList<CatalogEntry> CompiledSpecs(SpecRegistry registry, MotivRulesOptions options) =>
        [.. registry.Entries.Select(entry => new CatalogEntry(
            entry.Name,
            options.ResolveModelId(entry.ModelType),
            entry.MetadataType.Name,
            entry.IsAsync,
            entry.Description,
            PropositionOrigin.Compiled,
            Parameters(entry)))];

    /// <summary>
    /// A parameterised registration's declarations, in order, or <c>null</c> for a plain one.
    /// The type name is lowercased to match the <c>parameterDeclaration.type</c> enum in
    /// <c>rule.v1.json</c>, so one vocabulary spans the schema, the DSL and the catalog.
    /// </summary>
    private static IReadOnlyList<CatalogParameter>? Parameters(SpecRegistryEntry entry) =>
        entry.Parameters is null
            ? null
            : [.. entry.Parameters.Select(parameter => new CatalogParameter(
                parameter.Name,
                parameter.Type.ToString().ToLowerInvariant(),
                parameter.HasDefault ? parameter.DefaultValue : null))];

    /// <summary>
    /// The layered listing: <see cref="PropositionSet.Propositions"/> already folds compiled,
    /// overridden and authored definitions into one effective set.
    /// </summary>
    private static IReadOnlyList<CatalogEntry> EffectiveSpecs(
        PropositionSet propositions, SpecRegistry registry) =>
        [.. propositions.Propositions
            .Where(Resolves)
            .Select(entry => new CatalogEntry(
                entry.Name, entry.ModelType, entry.MetadataType, entry.IsAsync,
                entry.Description, entry.Origin, CompiledParameters(entry, registry)))];

    /// <summary>
    /// The declarations of the compiled registration behind a proposition, or <c>null</c> when there
    /// is none to report — an authored or overridden proposition's behaviour comes from a document,
    /// not an argument contract, so it reports no parameters even where a compiled entry survives
    /// beneath it.
    /// </summary>
    private static IReadOnlyList<CatalogParameter>? CompiledParameters(
        PropositionEntry entry, SpecRegistry registry) =>
        entry.Origin == PropositionOrigin.Compiled && registry.Find(entry.Name) is { } spec
            ? Parameters(spec)
            : null;

    /// <summary>
    /// Whether a proposition still resolves to a spec. A quarantined authored proposition resolves
    /// to nothing, so it is not listed; a quarantined override is, reported as the compiled spec
    /// still resolving beneath it.
    /// </summary>
    private static bool Resolves(PropositionEntry entry) =>
        entry.Quarantine.Count == 0 || entry.Origin != PropositionOrigin.Authored;

    private static void MapRuleEndpoints(
        RouteGroupBuilder group,
        RuleSet rules,
        ChangeRequestSet? governance,
        MotivRulesOptions options,
        JsonSerializerOptions json)
    {
        group.MapGet("/rules", () =>
            Results.Json(rules.Rules
                .Select(rule => new RuleListEntry(
                    rule.Name,
                    options.ResolveModelId(rule.ModelType),
                    rule.MetadataType.Name,
                    rule.IsAsync,
                    rule.IsPolicy,
                    rule.Version,
                    rule.Description,
                    rule.Quarantine))
                .ToArray(), json));

        group.MapGet("/rules/{name}", (string name) =>
        {
            // FindEntry serves document and version from a single coherent snapshot.
            if (rules.FindEntry(name) is not { } entry)
                return UnknownRule(name, json);

            return Results.Json(
                new RuleGetResponse(
                    EndpointResponses.DocumentElement(entry.DocumentJson), entry.Version, entry.Quarantine),
                json);
        });

        group.MapPut("/rules/{name}", async (string name, RulePutRequest request, HttpContext http) =>
        {
            if (GrantGate.Refuse(http, GrantVerb.Publish, name, json) is { } refusal)
                return refusal;

            if (request.Document.ValueKind == JsonValueKind.Undefined)
                return EndpointResponses.MissingDocument(json);

            if (request.BaseVersion <= 0)
                return EndpointResponses.NonPositiveBaseVersion(json);

            var documentJson = request.Document.GetRawText();

            // Grants first, exactly as before — the gate governs *what* may be published, not *who*
            // may ask. Then the write itself, which with governance registered runs inside the gate
            // check rather than beside it: same core, same outcome, one execution.
            return governance is null
                ? ToResult(
                    await rules.UpdateAsync(
                        name, documentJson, request.BaseVersion, ProvenanceOf(http, request.ChangeNote),
                        http.RequestAborted),
                    name, json)
                : await MotivGovernanceEndpoints.GovernedRuleWrite(
                    governance, http, json, DirectWriteOperation.RuleUpdate,
                    name, documentJson, request.BaseVersion,
                    written => ToResult(written, name, json), request.ChangeNote);
        });

        group.MapDelete("/rules/{name}", async (string name, int baseVersion, HttpContext http) =>
        {
            if (GrantGate.Refuse(http, GrantVerb.Publish, name, json) is { } refusal)
                return refusal;

            if (baseVersion <= 0)
                return EndpointResponses.NonPositiveBaseVersion(json);

            // A rule is never removed, only reverted to its default — which the gate is shown as a
            // null document, the same shape a proposition withdrawal takes.
            return governance is null
                ? ToResult(
                    await rules.RevertAsync(name, baseVersion, ProvenanceOf(http), http.RequestAborted),
                    name, json)
                : await MotivGovernanceEndpoints.GovernedRuleWrite(
                    governance, http, json, DirectWriteOperation.RuleRevert,
                    name, documentJson: null, baseVersion,
                    written => ToResult(written, name, json));
        });
    }

    /// <summary>
    /// Who an ungoverned rule write is attributed to in the version log, and the caller's optional
    /// reason. The author is read through <see cref="PrincipalIdentity.Subject"/>, the same way the
    /// governed path reads its author, so one request is attributed identically whether or not
    /// governance is mounted.
    /// </summary>
    private static RuleChangeProvenance ProvenanceOf(HttpContext http, string? changeNote = null) =>
        new(PrincipalIdentity.Subject(http.User), changeNote);

    private static IResult ToResult(RuleUpdateResult outcome, string name, JsonSerializerOptions json) =>
        outcome.Outcome switch
        {
            RuleUpdateOutcome.Updated => Results.Json(new RulePutResponse(outcome.Version), json),
            RuleUpdateOutcome.VersionConflict => Results.Json(new RuleConflictResponse(outcome.Version), json, statusCode: 409),
            RuleUpdateOutcome.Invalid => Results.Json(new ValidationResponse(outcome.Errors), json, statusCode: 400),
            _ => UnknownRule(name, json)
        };

    private static JsonElement ToSchema(JsonSerializerOptions options, Type type)
    {
        // The schema exporter refuses to populate a missing resolver, so export from a copy
        // carrying the same reflection-based default the serializer itself populates at first
        // use — every other setting is copied, keeping schema and binding behavior aligned.
        var schemaOptions = options.TypeInfoResolver is null
            ? new JsonSerializerOptions(options) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }
            : options;
        return JsonSerializer.SerializeToElement(schemaOptions.GetJsonSchemaAsNode(type));
    }

    private static IResult UnknownModelType(string modelType, JsonSerializerOptions json) =>
        Results.Json(new ErrorResponse($"Unknown model type '{modelType}'."), json, statusCode: 400);

    private static IResult UnknownRule(string name, JsonSerializerOptions json) =>
        Results.Json(new ErrorResponse($"Unknown rule '{name}'."), json, statusCode: 404);
}
