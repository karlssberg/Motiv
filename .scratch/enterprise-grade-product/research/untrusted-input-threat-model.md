# Threat model — the rule document parser and binder as untrusted-input surfaces

Research findings for ticket `05-untrusted-input-threat-model`. Investigated against the code at
worktree `async-specs-policies-valuetask-8d5970`, plus empirical tests against the live
`Motiv.RulesEngine.Sample` host (net10.0, Release). All `file.cs:line` citations are to that tree.

## Summary — the three things that matter

1. **CRITICAL: an unauthenticated `POST /api/rules/evaluate` reliably kills the host with an
   uncatchable `StackOverflowException`.** Not via JSON nesting — that is well defended — but via a
   *flat* `and`/`or` operand array. ~1,640 operands (a ~49 KB request) is enough. The binder folds
   the array into a left-nested result tree with `Aggregate` (`RuleBinder.cs:128`), and evaluating
   it walks that tree with a non-tail recursion (`BooleanResultBase.cs:104-111`) that overflows the
   stack. Confirmed empirically: the process died and did not restart. The depth limit
   (`MaxDocumentDepth = 64`) does **not** bound array *width*; the width is bounded only by
   `MaxNodeCount = 10,000`, which is ~6× the crash threshold.

2. **HIGH: nested higher-order quantifiers are a memory/CPU/response-size amplifier on the same
   endpoint.** `k` quantifiers over an `n`-element collection is `k·n` evaluations and a response
   that grows with the full justification tree. Measured: `k=n=2000` produced a **200 MB** response
   in ~14 s from a 146 KB request (~1,400× body amplification); pushing further crashed the host
   (the same stack overflow, plus memory pressure). There is no timeout, no cancellation token
   threaded into evaluation, and no response cap.

3. **The client-side DSL (`ui/packages/rules-core/src/dsl/`) is never used server-side.** JSON is the
   only server input. Its recursive-descent parser (`parser.ts`) has no depth guard and will
   overflow the *browser tab's* stack on deeply nested input, but that is DoS-in-the-tab, not host
   compromise. Threat model: low.

The headline mitigation is cheap and belongs in one place: cap operand-array width (and total node
count far lower than 10,000), and evaluate rule documents on a bounded-depth / bounded-time path.

---

## Finding 1 — Flat operand array → uncatchable StackOverflowException (CRITICAL)

### What makes it a vulnerability

A `StackOverflowException` in .NET cannot be caught (since .NET 2.0 it terminates the process
immediately, bypassing `catch`, `finally`, and `try`). So any input that reliably drives unbounded
recursion on a request thread is a remote, unauthenticated kill switch for the whole host — every
in-flight request on every connection dies with it, and a supervisor must restart the process.

### The chain

1. **The parser's depth guard counts nesting, not width.** `ParseNode` increments `depth` by 1 per
   level and rejects `depth > MaxDocumentDepth` (`RuleDocumentParser.cs:79-82`, `479-490`). But an
   n-ary operator's operands are all siblings at `depth + 1` — see the `foreach` in
   `ParseBinaryOperator` (`RuleDocumentParser.cs:227-233`), where every array element is parsed at
   the same `depth + 1`. A `{"and":[leaf, leaf, … 10000 leaves]}` document is depth 2 and passes the
   depth check. Width is bounded only by `_nodeCount > MaxNodeCount` (`RuleDocumentParser.cs:484-487`),
   default **10,000** (`RuleSerializerOptions.cs:9`).

2. **The binder folds the flat array into a left-deep tree.** `BindComposition` does
   `children.Aggregate((left, right) => left.And(right))` (`RuleBinder.cs:116-135`). `k` operands
   become a left-nested chain of `AndBooleanResult` nodes ~`k` deep. Binding itself is iterative, so
   this step does not overflow (confirmed: `POST /validate` survived `k = 9,999`).

3. **Evaluation walks the tree with non-tail recursion.** `BooleanResultBase.UnderlyingAssertionSources`
   recurses into each operand's own `UnderlyingAssertionSources` via `SelectMany`
   (`BooleanResultBase.cs:104-111`). On a `k`-deep left-nested tree this recurses ~`k` frames deep.
   It is reached during result projection — `ResultSerializer.MapExplanation` /
   `ToEvaluationResult` (`ResultSerializer.cs:35-54`) and `Explanation` construction
   (`Explanation.cs:124`) — which the `/evaluate` delegate always runs
   (`MotivRulesOptions.cs:82-83`, `MotivRulesEndpoints.cs:77`).

### Reproduction (verified)

Host: `Motiv.RulesEngine.Sample`, Release, default options. Body:
`{"modelType":"customer","document":{"rule": {"and":[{"spec":"customer.is-active"}, … ×k]}}, "model":{"age":30,"isActive":true,"orderCount":1,"orders":[]}}`

Bisected result on `POST /api/rules/evaluate`:

| k (operands) | request size | outcome |
|---|---|---|
| 1,000 | 29 KB | 200 OK, ~0.05 s |
| 1,500 | 44 KB | 200 OK, ~0.11 s |
| **1,625** | 49 KB | **last surviving value** |
| **1,640** | 49 KB | **process terminated — `Stack overflow.`** |
| 2,000 | 58 KB | process terminated |

The crash frame repeats `Motiv.BooleanResultBase.get_UnderlyingAssertionSources` →
`Enumerable.SelectMany` → `ToArray` (from the host's own stderr; "Repeated 2468 times"). The exact
threshold depends on stack size and frames-per-level, so treat ~1,600 as indicative, not a
guaranteed floor. Note: `MaxNodeCount = 10,000` permits ~6× this, and the request (~49 KB) is far
under ASP.NET Core's default 30 MB body limit — so neither existing limit prevents it.

`POST /validate` with the same document returns 200 and does **not** crash, because validation binds
but never evaluates the result tree. The kill vector is specifically `/evaluate`. (`/evaluate`
requires a `model`, but a trivial one suffices.)

### Mitigation and cost

- **Cheapest, most robust: cap operand-array width and lower the total node budget.** Reject any
  n-ary operator with more than, say, 64–256 operands in `ParseBinaryOperator`, and cut
  `MaxNodeCount` to something like 500. Cost: one comparison in the parser plus a new
  `RuleError`; a handful of option/threshold tests. Does not touch Motiv core. This is the
  recommended fix — it removes the reachable path without depending on Motiv core changing.
- **Deeper, but core-wide: make the result-tree traversal iterative** (explicit stack) in
  `BooleanResultBase`. This removes the whole class of "wide document → deep result tree → overflow"
  and also protects the legitimately-deep (≤64) case, but it is a change to hot, well-tested core
  traversal code with justification/de-noising semantics to preserve — higher risk, wider blast
  radius. Worth doing eventually; not the first move.
- **Belt-and-braces: evaluate on a thread with a large, bounded stack** (`new Thread(..., maxStackSize)`)
  so an unexpected deep tree throws a *catchable* overflow on that thread. Awkward with async and
  still lets a wide-enough tree exhaust even a large stack — a backstop, not a fix.

A node-count/width cap alone closes this finding. The traversal rewrite is the principled follow-up.

---

## Finding 2 — Higher-order quantifier amplification (HIGH)

### What makes it a vulnerability

One small request becomes `k·n` spec evaluations and a response whose size tracks the full
justification tree, with no timeout, no cancellation, and no output cap. It is a
memory/CPU/bandwidth amplifier on the same unauthenticated `/evaluate` endpoint, and at the top end
it reaches the Finding 1 overflow as well.

### The chain

- A higher-order node quantifies over a registered collection selector: `BindHigherOrder` →
  `CollectionBinding.BindHigherOrder` → `HigherOrder.Build` (`RuleBinder.cs:138-150`,
  `CollectionBinding.cs:31-39`, `HigherOrder.cs`). Evaluating one quantifier evaluates the child
  spec once per element — `n` element evaluations for an `n`-element collection.
- Nothing stops a document from ANDing `k` such quantifiers, or nesting the model so `n` is large.
  Both `k` (node count) and `n` (elements in the posted `model`) are attacker-controlled. The full
  justification for every element is materialised into the response
  (`ResultSerializer.ToEvaluationResult`, `ResultSerializer.cs:31-42`).

### Reproduction (verified)

`k` copies of `{"asAllSatisfied":{"spec":"order.is-large"},"path":"orders"}` ANDed together, against
a `model` with `n` orders, `POST /evaluate`:

| k | n | evaluations | request | response | time |
|---|---|---|---|---|---|
| 100 | 100 | 10,000 | 7 KB | 0.5 MB | 0.01 s |
| 500 | 500 | 250,000 | 36 KB | 12.5 MB | 0.28 s |
| 1000 | 1000 | 1,000,000 | 73 KB | 50 MB | 1.1 s |
| 2000 | 2000 | 4,000,000 | 146 KB | **200 MB** | 13.9 s |
| 4999 | 4000 | ~20,000,000 | 352 KB | — | **host died at ~63 s** |

The 200 MB response from a 146 KB request is ~1,400× body amplification; a few concurrent such
requests exhaust memory regardless of the overflow.

### Mitigation and cost

- **Bound evaluation work and output.** Options that fall out of the same node-count cap as
  Finding 1, plus: a response-size ceiling in the `/evaluate` delegate, an evaluation timeout, and a
  cap on collection cardinality the higher-order path will process. Cost: moderate — a wrapper around
  `binding.Evaluate` and a couple of options.
- **Thread a `CancellationToken` and a request timeout through evaluation.** Today the `/evaluate`
  delegate takes no token and calls the synchronous, non-cancellable `spec.Evaluate(model)`
  (`MotivRulesOptions.cs:82`, `MotivRulesEndpoints.cs:63-89`), so even a client disconnect does not
  stop the work. Adding cancellation to the *sync* path is intrusive (Motiv's sync evaluate has no
  token); a pragmatic first step is a wall-clock budget enforced by the endpoint plus the node cap.
- **Rate limiting and request-size limits** (the fog patch this ticket feeds) reduce the blast
  radius but do not fix single-request amplification — the node/cardinality cap is the load-bearing
  control.

---

## Finding 3 — Client-side DSL is client-only; unbounded recursion is DoS-in-the-tab (LOW)

### Established: JSON is the only server input

The DSL parser (`ui/packages/rules-core/src/dsl/parser.ts`, `lexer.ts`) is consumed exclusively by
the browser SPA. `parse` / `printInline` / `print` are imported only from `ui/apps/demo/src/**`
components (`builder/PendingSlot.tsx`, `builder/RuleDslStrip.tsx`, `builder/NodeDsl.tsx`,
`dsl/useDslSync.ts`, …) and from the package's own tests. `@motiv/rules-core` is a headless
TypeScript package; nothing in the .NET server references it. The server accepts a `RuleDocument`
as **JSON** (`ValidateRequest.Document` / `EvaluateRequest.Document`, `RulesContracts.cs:41-51`) and
parses it with `RuleDocumentParser`, never the DSL. So the DSL's threat model is XSS/DoS confined to
the authoring tab, not host compromise.

### Unbounded recursion does exist in `parser.ts`

`parseUnary` recurses on each leading `!` (`parser.ts:212-223`), `parseBinaryLevel` recurses through
precedence levels and operand runs (`parser.ts:249-276`), and `parseQuantifier` recurses into the
body (`parser.ts:110-152`) — with **no depth counter anywhere**. A pathological string (`!!!!…`, or
deeply nested `((((…`) overflows the JS engine's call stack and throws `RangeError: Maximum call
stack size exceeded`. Because `parse` is documented as "never throws" (`parser.ts:344-347`) and
callers only read `result.errors` (e.g. `NodeDsl.tsx:68-70`), that `RangeError` is unhandled and can
crash the React render. Impact is limited to the user's own tab. Worth a depth guard for robustness,
but not security-critical.

### Mitigation and cost

Add a depth counter to the recursive descent (increment in `parseUnary` / `parseBinaryLevel` /
`parseQuantifier`, error out past a limit), matching whatever server-side width/depth caps land.
Trivial and local. Priority low.

---

## Cleared hypotheses (checked and found safe — do not re-investigate)

- **JSON nesting → StackOverflow in the parser.** *Cleared.* `RuleDocumentParser` is recursive
  descent, but `ExceedsLimits` bails at `depth > MaxDocumentDepth` *before* recursing further
  (`RuleDocumentParser.cs:79-82`), and `JsonDocument.Parse` is given an explicit
  `MaxDepth = MaxDocumentDepth*2 + 4` reader limit (`RuleDocumentParser.cs:15-20`). Verified: raw
  `{"not": …}` nested 1,000,000 deep (8 MB) returns 400 in ~10 ms with the host alive; the STJ reader
  rejects it at its depth ceiling before the parser recurses. The nested path is well defended — the
  gap is width (Finding 1), not depth.
- **`RuleParameterSubstituter` blow-up.** *Cleared.* `Apply` recurses over `node.Children`
  (`RuleParameterSubstituter.cs:8-19`) — bounded by nesting depth (≤64), and a flat array is one
  level of iteration, not deep recursion. `Interpolate` is a single linear pass over each payload
  string with `IndexOf` (`RuleParameterSubstituter.cs:43-102`); no re-scanning of substituted text,
  so no substitution amplification. Values are formatted once (`Format`, line 104). No quadratic or
  exponential behaviour.
- **`RuleParameterResolver` blow-up.** *Cleared.* `Resolve` is `O(declarations)` plus an
  `O(declarations²)` surplus check (`declarations.All(...)` inside a `Where`,
  `RuleParameterResolver.cs:42`) — but declarations are parameter *declarations*, capped implicitly
  by node/document size and realistically tiny; not an attacker amplifier of concern next to
  Findings 1–2. `ToDictionary` reflects over supplied-object properties once (lines 17-27).
- **Async fan-out to outbound I/O via rule documents.** *Cleared for the rules endpoints.* There is
  **no async evaluate endpoint.** `/evaluate` always calls the synchronous `spec.Evaluate`
  (`MotivRulesOptions.cs:82`); `/validate?isAsync=true` only *validates* (binds), it does not run
  I/O (`MotivRulesEndpoints.cs:56-60`). Higher-order subtrees are always synchronous and an async
  spec inside one is a hard error (`AsyncRuleBinder.cs:149-166`), so the collection path cannot
  fan out to per-element I/O. The only async execution of Motiv specs is the app's own
  `/api/checkout` over **fixed compiled rules** (`Program.cs:107-120`), not user documents — and it
  *does* thread the request `CancellationToken`. So "one request → N outbound calls" is not
  reachable through user-supplied documents today. (If a future async `/evaluate` is added, re-open
  this: async `and`/`or` of N async spec references would fan out, and the sync evaluate path
  currently ignores cancellation.)
- **Cyclic proposition references → infinite binding recursion.** *Cleared.* `DependencyGraph.FindCycle`
  rejects cycles on `Create`/`Update` (`PropositionSet.cs:533-538`), and `Load` detects cycles in a
  hand-edited store and quarantines every member (`PropositionSet.cs:314-325`, `OrderByDependency`
  DFS with on-path detection, lines 446-475). The graph walks (`Reaches`, `DependentClosure`) are
  guarded by `visited` sets. (Those walks are themselves recursive and depth-bounded only by the
  proposition graph's own depth, but that graph is authored/persisted, not free-form request input,
  and cycle detection caps it — lower concern than Findings 1–2.)
- **`/evaluate` accepting an arbitrary model type.** *Cleared as an injection vector.* `modelType` is
  looked up in a fixed registration dictionary (`MotivRulesOptions.TryGetBinding`,
  `MotivRulesOptions.cs:94-95`); an unregistered id returns 400 (`MotivRulesEndpoints.cs:72-73`).
  The `model` JSON is deserialized to the *registered* CLR type and a shape mismatch becomes a 400
  `InvalidModelException`, not a 500 (`MotivRulesOptions.cs:71-81`). No arbitrary-type
  instantiation. (It is still an amplification *input* — `n` collection elements — see Finding 2.)

## Other security-relevant notes (in passing)

- **No authentication on any endpoint.** Confirmed — `MapMotivRules` mounts validate/evaluate/rules/
  propositions with no auth filter (`MotivRulesEndpoints.cs`), consistent with the map's standing
  note. Findings 1–2 are therefore reachable by any network caller. Auth narrows *who* can trigger
  them but does not fix the crash — an authenticated analyst can still overflow the host, so the
  node/width cap is needed regardless.
- **No request-size or rate limits configured.** The sample relies on ASP.NET Core's 30 MB default
  body limit, which is ~600× larger than the Finding 1 crash payload. Body-size limits alone are not
  a defence here.
- **Error messages leak some internals but not dangerously.** Parser errors echo the STJ
  `JsonException.Message` (`RuleDocumentParser.cs:24`) and type mismatches echo CLR type *names*
  (e.g. `RuleBinder.cs:85-87`, `MotivRulesOptions.cs:79`) — model/metadata short type names, not
  full assembly-qualified names, stack traces, or paths. Low severity; worth a pass before GA but not
  a finding.
- **`JsonFilePropositionStore` path handling** (`src/examples/Motiv.RulesEngine.Sample/JsonFilePropositionStore.cs`).
  The path is fixed at startup from configuration (`Program.cs:71-72`), never from request input —
  no path traversal from the API. The store rewrites the whole file per save and, on an unreadable
  file, logs and continues with an empty set so the *next write overwrites the unread file*
  (documented in its own remarks, lines 9-22, 55-84). That is a durability/data-loss footgun for a
  real deployment, not a request-reachable vuln; flagged because the map calls this store out as the
  persistence seam an enterprise would replace.

---

## Prior art — how comparable engines bound evaluation

The instructive split is **total language vs. bounded interpreter**. Motiv's document format is
close to total already (no loops, no user-defined recursion — composition only); the crash is an
*implementation* recursion over the result tree, not a language-expressiveness problem. That points
the fix at structural caps + iterative traversal rather than a step budget.

- **OPA / Rego** — the strongest model. Rego **prohibits recursion at compile time**: the compiler
  rejects any recursive rule definition, which is what gives evaluation its termination guarantee and
  makes static analysis possible. On top of that, OPA exposes a **query timeout**
  (`timeout_seconds`) as a runtime backstop for pathological-but-terminating queries. The known
  escape hatch is *dynamic* data references that smuggle recursion past compile-time analysis
  (open-policy-agent/opa#1565, #6428), which is precisely the "structural guarantee can be defeated
  by a dynamic path" lesson relevant to Motiv's spec/proposition references.
- **JsonLogic / json-rules-engine** — the permissive end. Both recurse over the rule tree depth-first
  with **no built-in depth limit, step budget, or timeout**; `json-logic-engine` adds stricter error
  handling for untrusted input but not resource bounds. The ecosystem's answer to untrusted JSON is
  an **external control — a WAF-level max-JSON-depth check** (e.g. Citrix/NetScaler JSON DoS
  protection). Takeaway: if Motiv stays permissive it inherits exactly this problem and must supply
  the bound itself, because there is no language-level guarantee to lean on.
- **Drools / KIE** — a stateful production-rule (Rete) engine. It runs to fixpoint —
  `fireAllRules()` / `fireUntilHalt()` — with **no evaluation timeout by default**; termination is
  the rule author's responsibility, and infinite-loop protection is opt-in (e.g. loop/no-loop
  controls, or an external halt). Confirms that mature engines *without* a total language push the
  bound onto operators, which is fragile.

**Bearing on Motiv's choice.** Motiv already has the property Rego works hard for — the document
grammar has no loops or user recursion. The right posture is therefore: (1) keep the format total;
(2) add **structural caps** (operand-array width, total node count, collection cardinality) at parse
time — the JsonLogic/WAF lesson, but enforced in-library where the structure is known rather than at
a proxy; and (3) add an **evaluation timeout / cancellation** as the OPA-style runtime backstop for
work that is bounded in shape but expensive in size (Finding 2). The one thing none of the prior art
excuses is the *uncatchable* crash: a bounded, iterative result-tree traversal (Finding 1's deeper
fix) is what turns "expensive" into "merely slow," which every one of these engines assumes.

Sources: [OPA Policy Language](https://www.openpolicyagent.org/docs/policy-language),
[OPA arbitrary recursion via dynamic data #1565](https://github.com/open-policy-agent/opa/issues/1565),
[OPA infinite-recursion crash #6428](https://github.com/open-policy-agent/opa/issues/6428),
[json-rules-engine](https://github.com/CacheControl/json-rules-engine),
[json-logic-engine](https://www.npmjs.com/package/json-logic-engine),
[NetScaler JSON DoS protection](https://docs.netscaler.com/en-us/citrix-adc/current-release/application-firewall/json-content-protection/json-dos-protection.html),
[Drools rule engine docs](https://docs.drools.org/latest/drools-docs/drools/rule-engine/index.html).
