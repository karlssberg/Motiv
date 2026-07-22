using Motiv;
using Motiv.Serialization;
using Motiv.Serialization.AspNetCore;

// Seam: the spec catalog. Register each spec under a stable name — rule documents
// reference specs by these names. Descriptions surface in the /catalog response.
var registry = new SpecRegistry()
    .Register(
        "is-active",
        Spec.Build((Customer c) => c.IsActive)
            .WhenTrue("customer is active")
            .WhenFalse("customer is inactive")
            .Create(),
        "Whether the customer account is active")
    .Register(
        "is-adult",
        Spec.Build((Customer c) => c.Age >= 18)
            .WhenTrue("customer is an adult")
            .WhenFalse("customer is a minor")
            .Create(),
        "Whether the customer is 18 or older")
    .Register(
        "has-orders",
        Spec.Build((Customer c) => c.OrderCount > 0)
            .WhenTrue("customer has orders")
            .WhenFalse("customer has no orders")
            .Create(),
        "Whether the customer has placed at least one order")
    .Register(
        "is-large-order",
        Spec.Build((Order o) => o.Total >= 100m)
            .WhenTrue("order is large")
            .WhenFalse("order is small")
            .Create(),
        "Whether an individual order total is 100 or more")
    // Seam: async specs register like sync ones; documents referencing them load via async
    // rules. The same spec instance also serves as FraudScreeningRule's compiled default.
    .Register(
        "passes-credit-check",
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

// Seam: live rules. Each AddRule enrolls a sealed rule class as a DI singleton and in the
// RuleSet behind GET/PUT/DELETE /api/rules/rules — the app executes the same instances the
// UI hot-swaps, with optimistic-concurrency protection on writes.
builder.Services.AddMotivRules(registry, options)
    .AddRule<CanCheckoutRule>()
    .AddRule<FraudScreeningRule>()
    .AddRule<LoyaltyDiscountRule>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

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
});

app.MapFallbackToFile("index.html");

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
