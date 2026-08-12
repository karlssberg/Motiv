using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Motiv;
using Motiv.RulesEngine.Sample;
using Motiv.Serialization;
using Motiv.Serialization.AspNetCore;

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
            builder.Services.AddSingleton<IGrantSource>(new JsonFileGrantSource(grantsPath));
            break;
        default:
            throw new InvalidOperationException(
                $"Unknown Motiv:Grants:Source '{grantSource}'. Expected 'app' or 'claims'.");
    }
}
builder.Services.AddAuthorization();

// Seam: authored propositions. AddPropositions enables the propositions endpoints and points them
// at a store. Propositions load before rule defaults bind, so a rule's default document may
// reference one. The path is configurable so a container can mount it on a volume.
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

builder.Services.AddMotivRules(registry, options)
    .AddPropositions(new JsonFilePropositionStore(propositionsPath))
    .AddGovernance(new JsonFileGateStore(gatePath))
    .AddRule<CanCheckoutRule>()
    .AddRule<FraudScreeningRule>()
    .AddRule<LoyaltyDiscountRule>();

var app = builder.Build();

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

// Seam: a rule being *used*. Handles arrive by type via DI — no name strings, and each
// Evaluate/EvaluateAsync reads an immutable snapshot, so a concurrent PUT never tears a result.
var resultSerializer = new ResultSerializer();
app.MapPost("/api/checkout", async (
    CanCheckoutRule canCheckout,
    FraudScreeningRule fraudScreening,
    Customer customer,
    CancellationToken cancellationToken) =>
{
    var eligibility = canCheckout.Evaluate(customer);
    var screening = await fraudScreening.EvaluateAsync(customer, cancellationToken);
    return Results.Json(new CheckoutResponse(
        eligibility.Satisfied && screening.Satisfied,
        resultSerializer.ToEvaluationResult(eligibility),
        resultSerializer.ToEvaluationResult(screening)),
        options.JsonSerializerOptions);
})
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
    if (grants is not JsonFileGrantSource store) return Results.NotFound();
    if (!grants.IsAdministrator(http.User)) return Results.StatusCode(403);
    return Results.Json(store.All);
});
admin.MapPost("", (HttpContext http, IGrantSource grants, [FromBody] GrantRecord record) =>
{
    if (grants is not JsonFileGrantSource store)
        return Results.NotFound();
    if (!grants.IsAdministrator(http.User))
        return Results.StatusCode(403);
    store.Add(record);
    return Results.NoContent();
});
admin.MapDelete("", (HttpContext http, IGrantSource grants, [FromBody] GrantRecord record) =>
{
    if (grants is not JsonFileGrantSource store) return Results.NotFound();
    if (!grants.IsAdministrator(http.User)) return Results.StatusCode(403);
    return store.Remove(record) switch
    {
        GrantRemovalOutcome.Removed => Results.NoContent(),
        GrantRemovalOutcome.LastAdminister => Results.Json(
            new { error = "cannot remove the last administer grant" }, statusCode: 409),
        _ => Results.NotFound()
    };
});

app.MapFallbackToFile("index.html", staticFiles);

app.Run();

/// <summary>The demo model that rules are evaluated against.</summary>
public sealed record Customer(int Age, bool IsActive, int OrderCount, IReadOnlyList<Order>? Orders = null);

/// <summary>An individual order placed by a <see cref="Customer"/>, used for higher-order collection rules.</summary>
public sealed record Order(decimal Total);

/// <summary>The outcome of a checkout attempt: both live rules and the combined verdict.</summary>
public sealed record CheckoutResponse(
    bool Approved,
    RuleEvaluationResult<string> Eligibility,
    RuleEvaluationResult<string> Screening);

/// <summary>Exposes the entry point to WebApplicationFactory-based integration tests.</summary>
public partial class Program;
