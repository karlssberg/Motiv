---
title: Rule Snapshots
description: Motiv.Serialization.Snapshots and RuleSnapshot — a rule and the proposition rows a reproduction handed back, bound against your compiled registry the way the reproduction bound them, as the one dependency a generated test has.
---

A test generated from a reproduction must run what production ran: the rule document at the
version that decided, over the proposition versions it resolved through, on the compiled specs
the test already has. `RuleSnapshot`, in the **`Motiv.Serialization.Snapshots`** package, is that
binding — and nothing else. No store, no host, no decision log.

```csharp
[Fact]
public void Decision_8f3c_loyalty_discount_v7_reproduces()
{
    // Reproduced from decision 8f3c… (correlation 2f1a…) at 2026-09-18T10:42Z.
    // Fidelity: Exact. Rule loyalty-discount v7, customer.is-active v3, customer.is-eligible v5.
    var snapshot = RuleSnapshot.FromJson(
        rule: """{ "audited": true, "rule": { "spec": "customer.is-eligible" } }""",
        propositions:
        [
            """{ "name": "customer.is-eligible", "version": 5, "modelType": "customer", "document": { "rule": { "and": [ { "spec": "customer.is-active" }, { "spec": "customer.has-orders" } ] } } }""",
        ]);

    var model = new Customer { Id = "4711", Tier = "gold", Orders = 3 };

    var result = snapshot.Bind<Customer>(Specs.Registry).Evaluate(model);

    result.Satisfied.ShouldBeFalse();
    result.Assertions.ShouldBe(["customer is active", "fewer than five orders"]);
}
```

## What `Bind` Does

The proposition rows bind, in dependency order, into an overlay layered *over the registry*: a row
shadows the registry's entry of the same name — so a test keeps deciding as the reproduction did
after the compiled `customer.is-eligible` moves on — and everything the rows do not name resolves
through the compiled specs. It is the reproducer's own binder, so a snapshot and its reproduction
cannot drift. Then the rule document binds against that layered source and the spec comes back.

- A row that will not bind is a line in `Warnings` and its dependents stay unbound; the rule still
  binds if it does not need them, and throws `RuleSerializationException` if it does.
- A snapshot binds one model type: `TModel` is registered under the rows' `modelType` id. Rows
  spanning several ids throw `InvalidOperationException` naming them — that snapshot needs the
  host's own registrations, which is the reproducer's job, not a test's.
- `BindAsync<TModel>` is the twin for a rule that composes async specs.

## Where the Rows Come From

Exactly as `reproduce_decision` and `get_rule` return them: `name`, `version`, `modelType`,
`document` (an object, or the document as a string) and an optional `description`. A generated
test pastes them.

- The assertions in the generated test are the logged outcome, so the test is green first when
  fidelity was `Exact`; the developer edits the expectation to what *should* have happened and
  works from red.
- An unresolved model: the model line throws `NotImplementedException` naming the key, the
  justification tree sits in a comment, and the test is skipped with the fidelity reason.
- With scenarios, the agent writes a theory over every stored scenario plus the reproduced
  decision, asserting `expectedSatisfied` where set and the live verdict where not.
