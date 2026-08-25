using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Motiv;
using Motiv.RulesEngine.Sample;
using Motiv.Serialization;
using Motiv.Serialization.AspNetCore;
using Motiv.Serialization.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

// Seam: the spec catalog. Register each spec under a stable name — rule documents
// reference specs by these names. Descriptions surface in the /catalog response.
var registry = new SpecRegistry()
    .Register(
        "customer.is-active",
        Spec.Build((Customer c) => c.IsActive)
            .WhenTrue("customer is active")
            .WhenFalse("customer is inactive")
            .Create(),
        "Whether the customer account is active")
    .Register(
        "customer.is-adult",
        Spec.Build((Customer c) => c.Age >= 18)
            .WhenTrue("customer is an adult")
            .WhenFalse("customer is a minor")
            .Create(),
        "Whether the customer is 18 or older")
    .Register(
        "customer.has-orders",
        Spec.Build((Customer c) => c.OrderCount > 0)
            .WhenTrue("customer has orders")
            .WhenFalse("customer has no orders")
            .Create(),
        "Whether the customer has placed at least one order")
    .Register(
        "order.is-large",
        Spec.Build((Order o) => o.Total >= 100m)
            .WhenTrue("order is large")
            .WhenFalse("order is small")
            .Create(),
        "Whether an individual order total is 100 or more")
    // Seam: async specs register like sync ones; documents referencing them load via async
    // rules. The same spec instance also serves as FraudScreeningRule's compiled default.
    .Register(
        "customer.passes-credit-check",
        DefaultSpecs.PassesCreditCheck,
        "Simulated async credit-bureau check");

// Seam: collections for higher-order rules. Registering a parent→collection selector
// lets a rule document quantify (asAllSatisfied / asAtLeastNSatisfied / …) over the
// elements at "orders". The selector is the only place the collection is resolved.
registry.RegisterCollection<Customer, Order>("orders", c => c.Orders ?? []);

// Seam: evaluable models. Each AddModel maps a CLR type to the stable id clients pass
// as `modelType` (and that surfaces in the catalog for specs and collections).
var options = new MotivRulesOptions()
    .AddModel<Customer>("customer")
    .AddModel<Order>("order");

var builder = WebApplication.CreateBuilder(args);

// Bind to $PORT when the environment supplies one and nothing more specific was asked for.
// ASP.NET Core reads ASPNETCORE_URLS and --urls but not PORT, whereas container platforms and
// local dev harnesses conventionally inject PORT and expect the app to follow — so without this
// the app quietly keeps its default port and the caller's assignment is ignored. Explicit
// configuration still wins: `builder.Configuration["urls"]` already reflects both --urls and
// ASPNETCORE_URLS at this point, so this only fills a gap rather than overriding an intent.
var assignedPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(assignedPort) && string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
{
    builder.WebHost.UseUrls($"http://localhost:{assignedPort}");
}

// Fail-closed identity wiring: the endpoints are secure by default, so the host must be told
// who supplies the principal — OIDC for real deployments, the dev identity for local evaluation.
// Anything enable-able by omission is a default-credentials vulnerability, so no identity means
// no startup.
var devIdentityEnabled = builder.Configuration.GetValue<bool>("Motiv:DevIdentity:Enabled");
var oidcAuthority = builder.Configuration["Motiv:Oidc:Authority"];

if (devIdentityEnabled
    && builder.Environment.IsProduction()
    && !builder.Configuration.GetValue<bool>("Motiv:DevIdentity:AllowInProduction"))
{
    throw new InvalidOperationException(
        "The Motiv dev identity is enabled in a Production environment. Set " +
        "Motiv:DevIdentity:AllowInProduction=true only if you accept every request being " +
        "authenticated as the dev superuser.");
}

if (!devIdentityEnabled && string.IsNullOrWhiteSpace(oidcAuthority))
{
    throw new InvalidOperationException(
        "No identity is configured and the Motiv endpoints are secure by default. Configure " +
        "OIDC (Motiv:Oidc:Authority, Motiv:Oidc:Audience) or explicitly enable the dev " +
        "identity (Motiv:DevIdentity:Enabled=true).");
}

if (devIdentityEnabled)
{
    builder.Services
        .AddAuthentication(DevIdentityHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, DevIdentityHandler>(DevIdentityHandler.SchemeName, null);
    builder.Services.AddHostedService<DevIdentityWarningService>();
    builder.Services.AddSingleton<IGrantSource, DevGrantSource>();
}
else
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.Authority = oidcAuthority;
            o.Audience = builder.Configuration["Motiv:Oidc:Audience"];
            o.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    // Keycloak nests realm roles under realm_access.roles; flatten them into role
                    // claims so the claims→prefix mapping (and ClaimTypes.Role consumers) see them.
                    if (context.Principal is { } principal)
                        KeycloakClaims.FlattenRealmRoles(principal);
                    return Task.CompletedTask;
                }
            };
            // Containers talk to Keycloak over http — dev/demo only.
            o.RequireHttpsMetadata = false;
        });

    // Seam: grant source. "app" (or unset) is the default — a mutable, file-backed store the
    // running app administers itself. "claims" (Task 9) reads grants straight out of the OIDC
    // token instead; this switch is where that branch slots in.
    var grantSource = builder.Configuration["Motiv:Grants:Source"];
    switch (grantSource)
    {
        case "claims":
            var claimsMapping = builder.Configuration.GetSection("Motiv:Grants:ClaimsMapping")
                .Get<List<ClaimsGrantMapping>>() ?? [];
            builder.Services.AddSingleton<IGrantSource>(new ClaimsGrantSource(claimsMapping));
            break;
        case "app":
        case null:
        case "":
            var grantsPath = builder.Configuration["Motiv:Grants:Path"]
                ?? Path.Combine(builder.Environment.ContentRootPath, "grants.json");
            var appStore = new JsonFileGrantSource(grantsPath);

            // Seam: cold-start elevation. Only the app-owned store can be bootstrapped this way — a
            // decorator, not a standing superuser, so it's wired only when the config key is present.
            var bootstrapSubject = builder.Configuration["Motiv:Bootstrap:Subject"];
            IGrantSource grantsSource = string.IsNullOrWhiteSpace(bootstrapSubject)
                ? appStore
                : new BootstrapGrantSource(appStore, bootstrapSubject);
            builder.Services.AddSingleton<IGrantSource>(grantsSource);
            break;
        default:
            throw new InvalidOperationException(
                $"Unknown Motiv:Grants:Source '{grantSource}'. Expected 'app' or 'claims'.");
    }
}
builder.Services.AddAuthorization();

// Seam: the importer's proposition source. The live propositions store is the EF-backed one wired
// below; this path is read only by StoreImport.CopyAsync (Motiv:Store:ImportFromJson) as the JSON
// file to carry history in from. Configurable so a container can point it at a mounted
// propositions.json left over from a pre-EF deployment.
var propositionsPath = builder.Configuration["Propositions:Path"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "propositions.json");

// Seam: live rules. Each AddRule enrolls a sealed rule class as a DI singleton and in the
// RuleSet behind GET/PUT/DELETE /api/rules/rules — the app executes the same instances the
// UI hot-swaps, with optimistic-concurrency protection on writes.
// Seam: governance. AddGovernance mounts the change-request surface and routes every direct write
// through the approval gate, so there is no way around the ceremony once one is configured. The
// gate's default is permissive — access is still locked by grants; only the ceremony is opt-in — so
// with no gate.json on disk the app behaves exactly as it did before governance existed.
var gatePath = builder.Configuration["Motiv:Gate:Path"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "gate.json");

// Seam: the importer's rule source. The live rule store is the EF-backed one wired below; this
// path is read only by StoreImport.CopyAsync (Motiv:Store:ImportFromJson) as the JSON file to
// carry rule history in from — the same rules.json a pre-EF deployment hot-swapped rules into.
var rulesPath = builder.Configuration["Rules:Path"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "rules.json");

// Seam: rule and proposition persistence. Both stores are now one SQLite database rather than two
// JSON files: the (Name, Version) primary key is enforced by the database, so two replicas racing a
// publish really do produce one 200 and one 409 rather than both reading a stale file and both
// believing they won. failFastOnQuarantine keeps its default (true): under an approval gate, booting
// quietly into a quarantined rule's compiled default — behaviour nobody approved — is worse than a
// demo that refuses to boot until a stale row is repaired.
var storeConnectionString = builder.Configuration["Motiv:Store:ConnectionString"]
    ?? $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "motiv-store.db")}";

builder.Services.AddMotivEntityFrameworkStore(store => store.UseSqlite(storeConnectionString));

// Seam: the decision log. The sink is the in-memory reference implementation -- enough to demonstrate
// the trail, and explicitly not enough for production, where the log must outlive the process and be
// bounded by a retention window. An adopter swaps in their own IDecisionSink here: a durable table, a
// SIEM, an outbox. It is registered as a singleton so the container drains the queue at shutdown.
//
// The capture posture is required, not optional: without one, a rule document marked "audited" does
// not bind at all. ReferenceOnly is the recommended production choice because it lets erasure and
// audit coexist -- erase cust-42 in the system of record and the decision survives without personal
// data, while replay correctly becomes impossible.
var decisionSink = new InMemoryDecisionSink();
builder.Services.AddSingleton(decisionSink);

builder.Services.AddMotivRules(registry, options)
    .AddDecisionLog(decisionSink, log =>
    {
        log.Backpressure = DecisionBackpressure.Block;
        log.Capture.ReferenceOnly<Customer>(customer => customer.CustomerId ?? "anonymous");
    })
    .AddPropositions(provider => new EfPropositionStore(
        provider.GetRequiredService<IDbContextFactory<MotivStoreDbContext>>()))
    .AddGovernance(new JsonFileGateStore(gatePath))
    .AddRuleStore(provider => new EfRuleStore(
        provider.GetRequiredService<IDbContextFactory<MotivStoreDbContext>>()))
    .AddRule<CanCheckoutRule>()
    .AddRule<FraudScreeningRule>()
    .AddRule<LoyaltyDiscountRule>()
    // Seam: multi-instance convergence. Each store operation opens its own context, so two
    // processes over one database behave like two replicas; AddRefresh polls for another replica's
    // publish and rebuilds this one, so docker compose up actually converges instead of
    // demonstrating a feature that does nothing.
    .AddRefresh();

// Seam: break-glass. AddGovernance already registered BreakGlass.Off; a host that wants the 3am
// escape overrides it here with AddSingleton, which — DI being last-registration-wins — beats the
// TryAddSingleton above. Deploy-time only: this reads appsettings/environment, never an in-app
// toggle, so turning it on requires ops access to the host's configuration, not a grant.
var breakGlassEnabled = builder.Configuration.GetValue<bool>("Motiv:BreakGlass:Enabled");
if (breakGlassEnabled)
{
    var breakGlassExpiresUtc = builder.Configuration.GetValue<DateTimeOffset?>("Motiv:BreakGlass:ExpiresUtc");
    builder.Services.AddSingleton(new BreakGlass(true, breakGlassExpiresUtc));
    builder.Services.AddHostedService<BreakGlassWarningService>();
}

var app = builder.Build();

// The schema, created in place for the zero-config path: `docker compose up` needs no migration
// step and no database server. A production host applies adopter-owned migrations instead —
// EnsureCreated deliberately does not write a migrations-history table, which is the same split
// ASP.NET Core Identity draws. Two instances starting together race the creation, so it goes
// through StoreSchema, which verifies all three tables and only continues past a failure that
// left the schema complete — see StoreSchema for what it refuses to swallow.
await using (var bootstrap = app.Services.GetRequiredService<IDbContextFactory<MotivStoreDbContext>>()
                 .CreateDbContext())
{
    await StoreSchema.EnsureCreatedAsync(bootstrap, app.Logger);
}

// One-way migration off the JSON stores. Opt-in, and a no-op once the database holds anything, so
// leaving the flag on is harmless.
if (builder.Configuration.GetValue("Motiv:Store:ImportFromJson", false))
{
    var imported = await StoreImport.CopyAsync(
        new JsonFileRuleStore(rulesPath),
        app.Services.GetRequiredService<IRuleStore>(),
        new JsonFilePropositionStore(propositionsPath),
        app.Services.GetRequiredService<IPropositionStore>(),
        CancellationToken.None);

    app.Logger.LogInformation(
        "Store import: imported={Imported} ruleVersions={RuleVersions} propositions={Propositions}",
        imported.Imported, imported.RuleVersions, imported.Propositions);
}

// index.html references content-hashed bundles, so a stale cached shell points at assets that no
// longer exist after a redeploy — force revalidation on the shell while the hashed assets stay
// cacheable. Applied to both the static middleware (/, /index.html) and the SPA fallback below.
var staticFiles = new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (string.Equals(context.File.Name, "index.html", StringComparison.OrdinalIgnoreCase))
            context.Context.Response.Headers.CacheControl = "no-cache";
    }
};

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles(staticFiles);

// Seam: the endpoints. Mounts GET /catalog, POST /validate, POST /evaluate — plus the rule
// endpoints under /api/rules/rules — backed by the registry, options, and RuleSet from DI.
app.MapMotivRules("/api/rules");

// Seam: readiness. AddMotivRules registers a "ready"-tagged check that asks each store for its
// generation — the cheapest question that still proves the connection works. Filtered to that tag on
// purpose: a load balancer asking "may I send this replica traffic?" must not be answered by a check
// that reports a replica *behind* the store as unhealthy. That one is /health, below, where an
// operator reads it.
app.MapHealthChecks("/health/ready",
    new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .AllowAnonymous();

app.MapHealthChecks("/health").AllowAnonymous();

// Seam: a rule being *used*. Handles arrive by type via DI — no name strings, and each
// Evaluate/EvaluateAsync reads an immutable snapshot, so a concurrent PUT never tears a result.
var resultSerializer = new ResultSerializer();
app.MapPost("/api/checkout", async (
    CanCheckoutRule canCheckout,
    FraudScreeningRule fraudScreening,
    LoyaltyDiscountRule loyaltyDiscount,
    RuleSet rules,
    HttpContext http,
    Customer customer,
    CancellationToken cancellationToken) =>
{
    // One pin for the whole checkout. Two things follow: the three rules resolve against one
    // published world rather than three independent reads, and every decision record they leave
    // carries one correlation id -- because a checkout is one decision, not three.
    using var _ = rules.PinSnapshot(http.TraceIdentifier, http.User.Identity?.Name);

    var eligibility = canCheckout.Evaluate(customer);
    var screening = await fraudScreening.EvaluateAsync(customer, cancellationToken);
    var loyalty = loyaltyDiscount.Evaluate(customer);

    return Results.Json(new CheckoutResponse(
        eligibility.Satisfied && screening.Satisfied,
        resultSerializer.ToEvaluationResult(eligibility),
        resultSerializer.ToEvaluationResult(screening),
        resultSerializer.ToEvaluationResult(loyalty)),
        options.JsonSerializerOptions);
})
.RequireAuthorization();

// Seam: reading the trail. The answer to "why was this customer declined, on the 3rd, at 14:07?" --
// the question the whole feature exists for. A durable sink would page and filter here; the in-memory
// reference implementation just hands back what it holds.
app.MapGet("/api/decisions", (InMemoryDecisionSink sink) => Results.Json(new
{
    records = sink.Records,
    // A gap is evidence about the log rather than a decision, so it is reported beside the records
    // and never counted among them. Empty is the only healthy value.
    gaps = sink.Gaps
}, options.JsonSerializerOptions))
.RequireAuthorization();

// Seam: administration surface. Capabilities tells the client what it's allowed to render (an
// immutable source like the dev grant source has no administration UI); the grants group is the
// UI's CRUD surface over a mutable store, gated on IsAdministrator and hidden (404) entirely when
// the active source doesn't support administration.
app.MapGet("/api/admin/capabilities", (HttpContext http, IGrantSource grants) => Results.Json(new
{
    grantAdministration = grants.SupportsAdministration,
    administrator = grants.IsAdministrator(http.User),
    devIdentity = devIdentityEnabled
})).RequireAuthorization();

var admin = app.MapGroup("/api/admin/grants").RequireAuthorization();
admin.MapGet("", (HttpContext http, IGrantSource grants) =>
{
    if (TryGetMutableStore(grants) is not { } store) return Results.NotFound();
    if (!grants.IsAdministrator(http.User)) return Results.StatusCode(403);
    return Results.Json(store.All);
});
admin.MapPost("", (HttpContext http, IGrantSource grants, [FromBody] GrantRecord record) =>
{
    if (TryGetMutableStore(grants) is not { } store)
        return Results.NotFound();
    if (!grants.IsAdministrator(http.User))
        return Results.StatusCode(403);
    try
    {
        store.Add(record);
    }
    catch (ArgumentException ex)
    {
        // An unknown verb is a malformed request, not a server failure — refused with the
        // store's own message rather than surfacing the exception as a 500.
        return Results.Json(new { error = ex.Message }, statusCode: 400);
    }
    return Results.NoContent();
});
admin.MapDelete("", (HttpContext http, IGrantSource grants, [FromBody] GrantRecord record) =>
{
    if (TryGetMutableStore(grants) is not { } store) return Results.NotFound();
    if (!grants.IsAdministrator(http.User)) return Results.StatusCode(403);
    return store.Remove(record) switch
    {
        GrantRemovalOutcome.Removed => Results.NoContent(),
        GrantRemovalOutcome.LastAdminister => Results.Json(
            new { error = "cannot remove the last administer grant" }, statusCode: 409),
        _ => Results.NotFound()
    };
});

// Seam: the admin endpoints only ever need the mutable store, never the grant-source wrapper
// itself — this unwraps BootstrapGrantSource's decorator (and passes a bare JsonFileGrantSource
// through unchanged), so callers above check IsAdministrator on `grants` (the elevation-aware
// wrapper) while mutating through the store this returns.
static JsonFileGrantSource? TryGetMutableStore(IGrantSource grants) => grants switch
{
    JsonFileGrantSource store => store,
    BootstrapGrantSource bootstrap => bootstrap.Store,
    _ => null
};

app.MapFallbackToFile("index.html", staticFiles);

app.Run();

/// <summary>The demo model that rules are evaluated against.</summary>
/// <remarks>
/// <paramref name="CustomerId"/> is what the decision log keeps of a customer under the
/// <c>ReferenceOnly</c> capture posture — a key into the system of record, and nothing else. Optional,
/// so a caller that supplies none records as "anonymous" rather than failing: the log's job is to say
/// what it knows, including when that is nothing.
/// </remarks>
public sealed record Customer(
    int Age, bool IsActive, int OrderCount, IReadOnlyList<Order>? Orders = null, string? CustomerId = null);

/// <summary>An individual order placed by a <see cref="Customer"/>, used for higher-order collection rules.</summary>
public sealed record Order(decimal Total);

/// <summary>The outcome of a checkout attempt: both live rules and the combined verdict.</summary>
public sealed record CheckoutResponse(
    bool Approved,
    RuleEvaluationResult<string> Eligibility,
    RuleEvaluationResult<string> Screening,
    RuleEvaluationResult<string> Loyalty);

/// <summary>Exposes the entry point to WebApplicationFactory-based integration tests.</summary>
public partial class Program;
