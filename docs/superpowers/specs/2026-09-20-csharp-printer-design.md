# C# printer — Design

**Date:** 2026-09-20
**Status:** Implemented
**Parent:** `2026-09-20-decision-reproduction-mcp-design.md`, section 6. Slice 4 of 5.
**Plan:** `docs/superpowers/plans/2026-09-20-csharp-printer.md`

## Problem

Studio's promise is that a runtime rule can become compiled code once it has earned it. A
reproduction (slice 3) says what a decision ran under; nothing yet turned that document into the
C# a developer would check in. Without the printer, "adopt" means transcribing JSON by hand — the
step where the rules-engine complaint about logic leaking out of the codebase comes true.

## Decisions

1. **The printer walks the parsed document, not the UI.** `CSharpPrinter.Print(json, options)`
   parses with the same `RuleDocumentParser` the binder uses, so what it prints is what would
   bind — and every surface (reproduction, a future endpoint, the MCP) prints the same way.
2. **`SpecRegistry.Get<TModel>(name, args)` is the runtime handle.** Printed code with no compiled
   handle for a name resolves it at run time through the registry, with the same errors a document
   would get. `GetAsync` is the async twin. Both go through the existing binders, so the explanation
   conversion and argument resolution are the binder's, not a second copy.
3. **Handles and collections are options, never guessed.** `SpecHandles` maps a name to the C#
   that is that spec; `Collections` maps a path to its element type and selector. `ModelType` is
   required and prints by its simple name, with a `using` in class mode. A nested model type is
   the caller's problem and the doc says so.
4. **Higher-order texts mirror `HigherOrder.Build` verbatim.** The binder decorates every
   quantifier with fixed `WhenTrue`/`WhenFalse` texts; a print that differed would produce
   different assertions. `n` prints as a literal or the parameter it names, with the texts as
   interpolated strings in that case.
5. **Document interpolation is C# interpolation, with the holes renamed.** `{{` means the same in
   both, and a `{param}` hole prints as `{argument}` where the argument is the parameter's C#
   identifier (`min_orders` → `minOrders`), formatted as `RuleParameterSubstituter` formats the
   value: `true`/`false` for a boolean, the invariant culture for a number. Parameters become
   method arguments, required first then defaulted.
6. **Three stand-ins, each a `TODO` and a warning.** A collection with no handle prints a
   PascalCased member guess over `object`; object payloads print as their JSON in string payloads
   (the print is an explanation rule); an expression leaf prints `Spec.From` verbatim. Nothing
   throws for these — the warning list is the contract.
7. **Compositions fold left, and every binary result is parenthesised.** The binder aggregates
   n-ary operands left to right; so does the print, and the parentheses make `!` and `.AndAlso`
   unambiguous without a precedence table.
8. **`Reproduction.CSharp` and `CSharpWarnings` sit on the record.** The reproducer prints the
   pinned document with the registry's collections named by element type (no selector — the
   registry holds a delegate, not its source), every reference through `registry.Get`, and the
   registry's names as `KnownSpecs`, so a reference to a runtime proposition is a `TODO` and a
   warning rather than an `UnknownSpec` the first time the adopted code runs. A recorded revert
   prints nothing: there is no document.
9. **The fidelity corpus is embedded and compared on justification.** Nineteen documents under
   `Printing/Corpus/` plus Studio's `loyalty-discount.json`; each is bound, printed, compiled with
   Roslyn against Motiv and the test's public model, and evaluated over six customers.
   `Justification` equality is stricter than the spec's "assertions as sets" and is what proves
   the fold shape. The harness is `#if !NETFRAMEWORK`: the test project also builds net472 for CI,
   where there is no trusted-assemblies list.

## Rejected

- **Source-generating C# from JSON at build time.** The document is data that changes at run
  time; a generator would print what was checked in, not what decided.
- **Printing through `Type.FullName`.** Correct for nested and generic models, unreadable for the
  common case; the print is meant to be read.
- **Guessing `TModel` from the first referenced spec.** Wrong for a document over compiled specs
  of several model types, and silently so.
- **Emitting definitions in declaration order.** A definition may reference one declared after it
  (the binder links locals after the whole envelope parses and refuses only cycles), and C# refuses
  a local used before its declaration. Locals are emitted in dependency order, declaration order as
  the tiebreak — and only for the definitions the root reaches at the rule's model: a definition
  used under a quantifier is bound over the element type, where it prints inline.

## Outcome

`CSharpPrinterTests` pins nineteen exact outputs; `PrinterFidelityTests` compiles and evaluates all
nineteen corpus documents — including a snake-case, boolean and number parameter interpolated in a
payload, a definition referencing a later one, and a local under a quantifier, the three shapes the
whole-branch review found the first sixteen missing; `SpecRegistryTests` covers `Get`/`GetAsync`; `DecisionReproducerTests`
covers `CSharp` and the revert case. `Motiv.Serialization.Tests` is green on net8, net9 and net10
and builds on net472; the library builds on netstandard2.0.
