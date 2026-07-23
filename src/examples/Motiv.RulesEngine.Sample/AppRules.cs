using Motiv;
using Motiv.Serialization;

/// <summary>
/// The compiled default implementations for the sample's live rules. Specs are plain values:
/// the same instance can serve as a rule's compiled default and be registered in the spec
/// catalog — <see cref="PassesCreditCheck"/> does both.
/// </summary>
public static class DefaultSpecs
{
    /// <summary>
    /// The compiled default for <see cref="CanCheckoutRule"/>: active AND adult.
    /// Deliberately duplicates the registry's "is-active"/"is-adult" specs rather than
    /// composing them — the compiled default stays self-contained, exactly as a rule
    /// document saved from the UI would rebuild it.
    /// </summary>
    public static SpecBase<Customer, string> CanCheckout { get; } =
        Spec.Build((Customer c) => c.IsActive)
            .WhenTrue("customer is active").WhenFalse("customer is inactive").Create()
            .AndAlso(Spec.Build((Customer c) => c.Age >= 18)
                .WhenTrue("customer is an adult").WhenFalse("customer is a minor").Create());

    /// <summary>
    /// The simulated credit check. This single instance is shared: registered in the spec
    /// catalog as "passes-credit-check" and used as the compiled default for
    /// <see cref="FraudScreeningRule"/>.
    /// </summary>
    public static AsyncSpecBase<Customer, string> PassesCreditCheck { get; } =
        Spec.BuildAsync(async (Customer c, CancellationToken ct) =>
            {
                // Simulated credit-bureau latency; replace with a real client call.
                await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
                return c.Age >= 18;
            })
            .WhenTrue("passes credit check")
            .WhenFalse("fails credit check")
            .Create();
}

/// <summary>
/// Gate for the checkout flow: hot-swappable via PUT /api/rules/rules/can-checkout. Each rule
/// is a sealed class so the type itself is the identity — inject the concrete type wherever
/// the rule is executed.
/// </summary>
public sealed class CanCheckoutRule() : Rule<Customer, string>(
    "can-checkout", DefaultSpecs.CanCheckout, "Gate for the checkout flow");

/// <summary>Async screening rule demonstrating an async spec composed into a live rule.</summary>
public sealed class FraudScreeningRule() : AsyncRule<Customer, string>(
    "fraud-screening", DefaultSpecs.PassesCreditCheck, "Simulated credit-bureau screening");

/// <summary>
/// Document-default rule: the embedded JSON is the version-1 implementation, authored or
/// exported from the UI and served from first resolve.
/// </summary>
public sealed class LoyaltyDiscountRule() : Rule<Customer, string>(
    "loyalty-discount", RuleDocuments.Embedded("loyalty-discount.json"),
    "Whether the customer qualifies for the loyalty discount");
