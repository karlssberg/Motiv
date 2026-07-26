---
title: Rule Classes
---

A rule is a named, versioned, hot-swappable decision handle. Concrete rules are declared as sealed
subclasses of one of four classes &mdash; the type itself is the identity, so call sites inject and
evaluate a concrete type rather than looking a rule up by name string.

```csharp
public class Rule<TModel, TMetadata> : RuleBase;
public class PolicyRule<TModel, TMetadata> : Rule<TModel, TMetadata>;
public class AsyncRule<TModel, TMetadata> : RuleBase;
public class AsyncPolicyRule<TModel, TMetadata> : AsyncRule<TModel, TMetadata>;
```

Each class offers two constructors &mdash; a compiled default or a
[document default](RuleDocuments.md):

```csharp
// Compiled default: a spec/policy built in code (the async classes take the async equivalents).
public Rule(string name, SpecBase<TModel, TMetadata> defaultSpec, string? description = null);
public PolicyRule(string name, PolicyBase<TModel, TMetadata> defaultPolicy, string? description = null);

// Document default: rule-document JSON, bound when the rule is added to a RuleSet.
public Rule(string name, RuleDocumentSource defaultDocument, string? description = null);
```

## Declaring Rules

```csharp
// Spec-flavoured, compiled default
public sealed class CanCheckoutRule() : Rule<Customer, string>(
    "can-checkout", DefaultSpecs.CanCheckout, "Gate for the checkout flow");

// Async, compiled default
public sealed class FraudScreeningRule() : AsyncRule<Customer, string>(
    "fraud-screening", DefaultSpecs.PassesCreditCheck, "Simulated credit-bureau screening");

// Document default: the embedded JSON is the version-1 implementation
public sealed class LoyaltyDiscountRule() : Rule<Customer, string>(
    "loyalty-discount", RuleDocuments.Embedded("loyalty-discount.json"),
    "Whether the customer qualifies for the loyalty discount");
```

## Evaluating

The sync classes expose `Evaluate()`; the async classes expose `EvaluateAsync()`:

```csharp
BooleanResultBase<TMetadata> Evaluate(TModel model);                       // Rule
PolicyResultBase<TMetadata> Evaluate(TModel model);                        // PolicyRule

ValueTask<BooleanResultBase<TMetadata>> EvaluateAsync(TModel model, CancellationToken cancellationToken = default); // AsyncRule
ValueTask<PolicyResultBase<TMetadata>> EvaluateAsync(TModel model, CancellationToken cancellationToken = default);  // AsyncPolicyRule
```

```csharp
var eligibility = canCheckout.Evaluate(customer);
var screening = await fraudScreening.EvaluateAsync(customer, cancellationToken);

eligibility.Satisfied;  // true
eligibility.Assertions; // ["customer is active", "customer is an adult"]
```

## Remarks

- **Evaluations read an immutable snapshot.** Each call captures the current implementation once;
  a concurrent update never tears an in-flight evaluation. The async classes forward the underlying
  spec's `ValueTask` directly off the snapshot, without an intermediate state machine.
- **Rules must be bound before use.** Evaluating a rule that has not been added to a
  [`RuleSet`](RuleSet.md) (or enrolled via [`AddMotivRules()`](AspNetCore.md)) throws an
  `InvalidOperationException` naming the rule.
- **Policy classes shadow the evaluation method.** `PolicyRule.Evaluate()` and
  `AsyncPolicyRule.EvaluateAsync()` narrow the result to the policy form, exactly as
  `PolicyBase` shadows `SpecBase` &mdash; a base-class-typed reference resolves to the
  spec-flavoured result.
- **Policy classes enforce policy documents.** Updating a `PolicyRule`/`AsyncPolicyRule` with a
  document whose bound root is a spec fails with the `PolicyRequired` error; the compiled-default
  constructors enforce the same guarantee at compile time by taking `PolicyBase`/`AsyncPolicyBase`.
- **Inspection properties.** Every rule exposes `Name`, `Description`, `ModelType`, `MetadataType`,
  `IsAsync`, `IsPolicy`, `Version` (starting at 1, incremented by every update or revert), and
  `DocumentJson` (the current document, or `null` while on a compiled default).

## Next Steps

- Add rules to a [`RuleSet`](RuleSet.md) to bind their defaults and enable updates.
- Use [`RuleDocuments`](RuleDocuments.md) to give a rule a document default.
- See [ASP.NET Core Integration](AspNetCore.md) for enrolling rules through DI.
