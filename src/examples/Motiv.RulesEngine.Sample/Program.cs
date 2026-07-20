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
        "Whether an individual order total is 100 or more");

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
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Seam: the endpoints. Mounts GET /catalog, POST /validate, POST /evaluate under
// /api/rules, backed by the registry (specs + collections) and the model registrations.
app.MapMotivRules("/api/rules", registry, options);
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>The demo model that rules are evaluated against.</summary>
public sealed record Customer(int Age, bool IsActive, int OrderCount, IReadOnlyList<Order>? Orders = null);

/// <summary>An individual order placed by a <see cref="Customer"/>, used for higher-order collection rules.</summary>
public sealed record Order(decimal Total);
