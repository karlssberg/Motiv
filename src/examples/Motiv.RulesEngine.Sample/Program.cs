using Motiv;
using Motiv.Serialization;
using Motiv.Serialization.AspNetCore;

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
        "Whether the customer has placed at least one order");

var options = new MotivRulesOptions().AddModel<Customer>("customer");

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapMotivRules("/api/rules", registry, options);
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>The demo model that rules are evaluated against.</summary>
public sealed record Customer(int Age, bool IsActive, int OrderCount);
