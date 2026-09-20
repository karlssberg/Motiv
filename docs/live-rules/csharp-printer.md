---
title: C# Printer
description: CSharpPrinter, CSharpPrintOptions, CSharpCollectionHandle, CSharpPrintedRule and SpecRegistry.Get — a rule document printed as the builder chain that compiles to the same specification, and the fidelity test that keeps it honest.
---

A rule document is a tree of references and operators over the specs you compiled in. The same
tree, written as C#, is a builder chain — and that is what `CSharpPrinter` prints. The point is
adoption: when a decision reproduced from the log is what the rule *should* keep doing, the printed
method is the code to check in, and the runtime rule can go back to its compiled default.

```csharp
var printed = CSharpPrinter.Print(documentJson, new CSharpPrintOptions
{
    ModelType = typeof(Customer),
    SpecHandles = new Dictionary<string, string> { ["customer.is-active"] = "Specs.IsActive" },
    Collections = new Dictionary<string, CSharpCollectionHandle> { ["orders"] = new("Order", "c => c.Orders") },
    ClassName = "LoyaltyDiscountRule",
    Namespace = "Shop.Rules",
});

printed.Source;     // the C#
printed.Warnings;   // every place it needs a person
```

## What It Prints

One static method, `SpecBase<TModel, string> Build(SpecRegistry registry, …)`, wrapped in a class
when `ClassName` is set:

| Document | Printed |
|---|---|
| `{ "spec": "name" }` | the handle from `SpecHandles`, else `registry.Get<TModel>("name")` — `GetAsync` for a name in `AsyncSpecs`, which also makes the method return `AsyncSpecBase` |
| `{ "spec": "name", "args": { "n": 3 } }` | `registry.Get<TModel>("name", new Dictionary<string, object?> { ["n"] = 3 })` |
| `{ "local": "key" }` | the local the definition became; under a quantifier, the definition printed inline over the element type, as the binder binds it there |
| `definitions` | `var key = …;` for every definition the root reaches at the rule's model, each declared after the definitions it references; keys become identifiers (`is-active` → `isActive`, `class` → `@class`, duplicates numbered) |
| `parameters` | method arguments after the registry — required first, then defaulted — typed `int`, `double`, `string`, `bool`, named as identifiers (`min_orders` → `minOrders`) |
| `and` / `or` / `xor` | `(a & b)`, `(a \| b)`, `(a ^ b)`, folded left for three or more operands exactly as the binder folds them |
| `andAlso` / `orElse` | `a.AndAlso(b)`, `a.OrElse(b)`, folded left |
| `not` | `!a` |
| `asAllSatisfied` … `asAtMostNSatisfied` with `path` | `Spec.Build(inner).AsAllSatisfied().WhenTrue("all satisfied").WhenFalse("not all satisfied").Create().ChangeModelTo<TModel>(selector)` — the texts are the binder's own; `n` as a literal or the parameter it names |
| `whenTrue` / `whenFalse` / `name` | `Spec.Build(x).WhenTrue("…").WhenFalse("…").Create()` or `.Create("name")`; a text with `{param}` prints as `$"…"` with each hole renamed to its argument and formatted as the substituter formats it — `{(strict ? "true" : "false")}` for a boolean, the invariant culture for a number |
| a root `name` | `return Spec.Build(root).Create("name");` |

`TModel` is never guessed. It is `CSharpPrintOptions.ModelType`: the rule's model type, or the
registry entry's. It prints by its simple name, and class mode adds a `using` for its namespace.

## Where It Needs a Person

Three things a document can say have no exact C#. Each prints a stand-in, marks it `TODO` in the
source, and adds a line to `Warnings`:

| Document | Stand-in |
|---|---|
| a higher-order `path` with no entry in `Collections` | `m => m.PascalCasedPath /* TODO: the collection registered at 'path' */` over element type `object` |
| object `whenTrue`/`whenFalse` payloads | the JSON as string payloads, `/* TODO: object payloads printed as strings */` — the print is an explanation rule |
| an `expression` leaf | `Spec.From((TModel m) => text) /* expression printed verbatim; not re-parsed */` |
| a `spec` name outside `KnownSpecs`, when that is set | `registry.Get<TModel>("name") /* TODO: 'name' is not a compiled spec */` — a runtime-authored proposition, which adopted code cannot resolve until it too is code |

A reproduction (see [replay](../decision-log/replay.md)) prints with the registry's collections
named by element type but no selector, and with the registry's names as `KnownSpecs`: a higher-order
rule reproduces with a `TODO` on the selector, and a reference to a runtime proposition with a
`TODO` on the call, each with a warning saying so.

## `SpecRegistry.Get`

Printed code that has no handle for a name resolves it at run time:

```csharp
registry.Get<Customer>("customer.is-active")
registry.Get<Customer>("customer.min-orders", new Dictionary<string, object?> { ["n"] = 2 })
registry.GetAsync<Customer>("customer.is-active-async")
```

`Get` returns the explanation spec a document's `spec` node binds to — arguments resolved against
a parameterised entry's declaration, a metadata spec converted to its explanation form — and
throws `RuleSerializationException` with the same `RuleError`s a document would get for an unknown
name, another model type, an async spec asked for synchronously, or arguments that do not match.

## The Fidelity Contract

`PrinterFidelityTests` in `Motiv.Serialization.Tests` holds a corpus of documents that between them
use every node kind. For each, the test binds the document, prints it, compiles the print with
Roslyn against Motiv and the test's model, and evaluates both over a scenario set: `Satisfied`,
`Assertions` and `Justification` must agree on every scenario. A document feature that lands
without a printer case fails there. The corpus is embedded under `Printing/Corpus/`; adding a
document is adding a file.
