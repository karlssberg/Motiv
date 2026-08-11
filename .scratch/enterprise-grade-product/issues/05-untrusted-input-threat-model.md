# Threat model — the DSL parser and rule binder as untrusted-input surfaces

Type: research
Status: resolved
Blocked by: —

Claimed by a `/research` subagent fired during the charting session (2026-08-03). Findings land at
`.scratch/enterprise-grade-product/research/untrusted-input-threat-model.md`.

## Question

Once authoring is authenticated but delegated to analysts, a rule document is **semi-trusted input
that executes**. The attack surface is unusually rich for a JSON API:

- `POST /api/rules/validate` and `/evaluate` accept arbitrary rule documents from any caller.
- `RuleDocumentParser` → `RuleBinder` turns a document into a composed spec — arbitrary nesting depth,
  arbitrary operand counts, higher-order quantifiers over collections.
- `@motiv/rules-core`'s DSL (`lexer.ts` 112 lines, `parser.ts` 366) parses free text client-side; the
  printed document then goes to the server.
- Higher-order rules quantify over collections resolved by registered selectors — a rule can
  amplify one request into a per-element evaluation.
- Async specs (`customer.passes-credit-check`) mean an evaluation can await outbound I/O.

**Research question:** what are the concrete abuse cases, and what does mitigation cost?

Investigate and report on:

1. **Algorithmic complexity.** Can a crafted document make binding or evaluation superlinear?
   Look specifically at nested higher-order quantifiers over collections, and at
   `RuleParameterSubstituter` / `RuleParameterResolver` for substitution blow-up.
2. **Stack depth.** Is `RuleDocumentParser` recursive-descent, and does deep nesting reach a
   `StackOverflowException` — which is *uncatchable* in .NET and kills the process? This is the
   highest-severity candidate: an unauthenticated `POST /validate` that reliably terminates the host.
3. **Amplification.** Higher-order rules over a large `orders` collection, and async specs that fan
   out to third-party I/O — can one request become N outbound calls?
4. **The client-side DSL.** Is the parser used anywhere server-side, or is JSON the only server
   input? If client-only, its threat model is XSS/DoS-in-the-tab, not host compromise.
5. **Prior art.** How do comparable engines (OPA/Rego, json-rules-engine, JsonLogic, Drools) bound
   evaluation? Depth limits, step budgets, timeouts, or a total language?

### Answer shape

A findings document: each abuse case with severity, a reproduction sketch where one exists, and the
mitigation with its cost. Do not implement mitigations. Capture on a throwaway `research/` branch and
link it from this ticket.

Feeds the fog patch "rate limiting and request-size limits", which cannot be sized until this lands.

## Answer

Full findings: [untrusted-input-threat-model.md](../research/untrusted-input-threat-model.md) (308 lines).

### The one that matters

**CRITICAL — uncatchable host crash from an unauthenticated request.** A flat operand array —
`{"and": [leaf ×k]}` — posted to `POST /api/rules/evaluate` terminates the process with a
`StackOverflowException`, which .NET cannot catch. Bisected empirically: survives k=1625, dies at
**k=1640**, a ~49 KB request.

The mechanism, verified independently against the cited code:

- `RuleDocumentParser.cs:481` guards `depth`, incremented **per nesting level**. Sibling *width* is
  unguarded; only `MaxNodeCount` (10,000) bounds it — roughly 6× above the crash point.
- `RuleBinder.cs:128` folds a flat array with `children.Aggregate((left, right) => left.And(right))`,
  producing a **left-deep tree**. k siblings become k levels.
- `BooleanResultBase.cs:104-111` — `UnderlyingAssertionSources` recurses through `Causes` via
  `SelectMany`, non-tail, during result serialization. k levels become k stack frames.

**The depth guard and the crash vector measure different things.** `/validate` does not crash: it
binds but never evaluates, so the recursive traversal is never reached.

**HIGH — higher-order amplification, same endpoint.** k quantifiers over n collection elements gives
k·n evaluations with no timeout, no cancellation, and no response cap; `/evaluate` calls the
synchronous, non-cancellable `spec.Evaluate` (`MotivRulesOptions.cs:82`). Measured: k=n=2000 turned a
146 KB request into a **200 MB response in ~14s**; larger crashed the host.

**LOW — the client DSL is client-only.** `ui/packages/rules-core/src/dsl/parser.ts` is imported only
by the browser SPA; JSON is the sole server input. Its recursive descent has no depth guard, but the
blast radius is the tab, not the host. This resolves sub-question 4 of the ticket.

### Cleared — do not re-investigate

JSON-nesting overflow (System.Text.Json `MaxDepth` plus the parser guard reject a 1M-deep document in
~10ms with the host alive) · `RuleParameterSubstituter` / `RuleParameterResolver` substitution
blow-up · async fan-out to outbound I/O (there is no async evaluate endpoint; higher-order is always
synchronous) · proposition reference cycles · arbitrary `modelType` (fixed dictionary, returns 400) ·
`JsonFilePropositionStore` path traversal (path fixed at startup).

### Prior art

Rego forbids recursion at compile time — a termination guarantee — plus a query timeout.
JsonLogic and json-rules-engine ship no bounds and lean on WAF-level JSON-depth checks. Drools runs
to fixpoint with no default timeout. The recommendation is to keep Motiv's total (non-Turing-complete)
document format and add **structural caps at parse time** — operand-array width, node count,
collection cardinality — plus an evaluation timeout.

### Mitigation cost

An operand-width cap is a few lines in `ParseBinaryOperator` and closes the critical finding without
touching Motiv core. Making the result-tree traversal iterative is the principled fix that removes the
whole uncatchable-crash *class*, and is the higher-risk follow-up. No mitigations were implemented.

### Graduated from this ticket

The fog patch "rate limiting and request-size limits" is now specifiable — see ticket 19.
