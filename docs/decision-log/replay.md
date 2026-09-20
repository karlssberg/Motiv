---
title: Replay
description: DecisionReproducer, Reproduction, ReproductionFidelity and the resolver seam — a logged decision run again under the rule and proposition versions that decided it, with a verdict on how exact the re-run was.
---

A decision record pins three anchors — the rule version, the build, and the version of every
authored proposition the rule resolved through — and keeps as much of the input as its capture
posture allows. Replay is what those anchors were for: `DecisionReproducer` turns a record back into
the documents that decided it, the model as far as it can be recovered, a fresh evaluation under
exactly those documents, and a **fidelity** verdict naming every anchor that could not be honoured.

```csharp
var reproduction = await reproducer.ReproduceAsync(decisionId, cancellationToken);

reproduction.Rule            // the StoredRuleVersion the record pinned, or null when the log lacks it
reproduction.Propositions    // the pinned StoredPropositionVersion rows that were bound
reproduction.Model           // Whole | Redacted | Resolved | Reference | Absent, and the value
reproduction.Replayed        // the re-evaluation, or null when nothing could run
reproduction.Fidelity.IsExact
reproduction.Fidelity.Notes  // each FidelityNote(Reason, Detail)
reproduction.CSharp          // the pinned document as C#, or null for a recorded revert
reproduction.CSharpWarnings  // where the print needs a person
```

## Registering It

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddRuleStore(store)                        // the rule version log
    .AddPropositions(propositionStore)          // the proposition version log
    .AddDecisionLog(sink, log =>
    {
        log.Capture.ReferenceOnly<Customer>(c => c.CustomerId);
        log.Resolve.Reference<Customer>((id, ct) => customers.FindAsync(id, ct));
    })
    .AddDecisionSource(sink);                   // the same sink, read back
```

`AddDecisionSource` registers `IDecisionSource` and a `DecisionReproducer` over it. Reading is
deliberately **not** on `IDecisionSink`: a sink that forwards to a SIEM has nothing to read back, so
a sink that keeps records implements `IDecisionSource` too — `InMemoryDecisionSink` and
`SqlDecisionSink` both do — and is registered a second time under that name. The reproducer needs
both version logs; resolving it without `AddRuleStore` or `AddPropositions` throws naming the one
that is missing.

## The Resolver Seam

`ReferenceOnly` capture stores a key and nothing else, which is what lets erasure and audit coexist.
It also means the log alone cannot replay: the model has to come back from your system of record.
`DecisionLogOptions.Resolve` is the mirror of `Capture` — one resolver per model type, registered
beside the posture that made it necessary:

```csharp
log.Capture.ReferenceOnly<Customer>(customer => customer.CustomerId);
log.Resolve.Reference<Customer>((customerId, ct) => customers.FindAsync(customerId, ct));
```

Nothing is registered by default. A reference-only decision on a host with no resolver reproduces
with `Model.Kind == Reference`, the key, no replay, and a `ModelUnresolved` note saying so. A
resolver that returns null means the subject is gone — erased, most likely — and the reproduction
says that too. A resolver that returns *today's* customer may replay to a different verdict than the
log holds, which is reported as `OutcomeDiverged` rather than hidden.

## What a Reproduction Honours

| Anchor | Honoured by | When it cannot be |
|---|---|---|
| `RuleVersion` | the row at that version from `IRuleStore.HistoryAsync`; a null document (a recorded revert) binds the compiled default | `RuleVersionMissing`; nothing replays |
| `BuildId` | compared with this host's build | `BuildMismatch`; the replay still runs, since the documents may well bind identically |
| `ReferencedPropositionVersions` | each pinned row from `IPropositionStore.HistoryAsync`, bound in dependency order into a transient overlay layered *over the live source*, so a pinned version shadows today's head and everything unpinned resolves as production does | `PropositionVersionMissing` (the name's live head stands in, when there is one) or `PropositionBindFailed` (dependents of a failed bind are left unbound, and nothing replays — the rule would otherwise resolve the name through today's head) |
| `Input` | `Whole` as captured; `Redacted` as captured, with `ModelRedacted`; `Reference` through the resolver | `ModelUnresolved` |

The live `RuleSet` and `PropositionSet` are read, never written. The overlay the pinned documents
bind into exists for the duration of the call.

## Fidelity

`Fidelity.IsExact` is true when the notes are empty. Every note is a value the generated test can
quote, and the reasons are:

| Reason | Cause |
|---|---|
| `RuleVersionMissing` | the rule log lacks the pinned version, or this host has no such rule; nothing replays |
| `BuildMismatch` | the decision was made on another build |
| `PropositionVersionMissing` | a pinned proposition version is not in the log; its live head was bound instead, when there was one |
| `PropositionBindFailed` | a pinned document no longer binds, or the rule's own document did not; nothing replays |
| `ModelRedacted` | the capture was a projection; fields the rule reads may be absent |
| `ModelUnresolved` | nothing was captured, no resolver is registered, or the resolver returned nothing |
| `OutcomeDiverged` | the replay decided differently from the log — loud on purpose |

## Two Things Worth Knowing

**A replay never records.** The reproducer asks the rule to bind the pinned document and evaluate
the bound spec directly, not through `Rule.Evaluate`. Nothing is written to the decision log, no
proposition pin is taken, and no telemetry span opens. Reproducing a decision a thousand times
leaves the log exactly as it was. For the same reason an `audited` document replays on a host that
has no decision log or capture posture at all — a read-only reproduction host needs the two version
logs and nothing else.

**A durable sink hands the model back as JSON.** A `Whole` or `Redacted` capture read from
`SqlDecisionSink` is a `JsonElement`, not the model. The reproducer rehydrates it to the rule's
model type through the host's `JsonSerializerOptions` — the ones on `MotivRulesOptions`, so your
converters apply — and a projection that lacks a field the rule reads deserializes as far as it
goes. An in-memory capture that is already the model type is used as it is.

## Over HTTP

With `AddDecisionSource`, `MapMotivRules` also maps, under its base path, `GET decisions`
(`correlationId`, `ruleName`, `satisfied`, `from`, `to`, `limit`; records of rules the caller may not
read are omitted, and `limit` counts records before that filter), `GET decisions/{id}` and
`GET decisions/{id}/reproduction` (`Read` on the record's rule, else `403`; `503` when no reproducer
is registered), and `GET rules/{name}/csharp?version=` (`204` for a version that ran compiled code;
version 1 is the rule's default, so a document default prints and a compiled one is `204`). A reproduction crosses the wire with the captured input and the replayed model as the
host's JSON — and a reference capture as its key and nothing else. The [MCP server](./mcp.md)
serves the same through tools, and a [snapshot](./snapshots.md) binds what came back.

## The C# Beside It

`Reproduction.CSharp` is the pinned rule document printed by the [C# printer](../live-rules/csharp-printer.md):
a static `Build(SpecRegistry registry, …)` in a class named after the rule, every compiled spec
resolved through `registry.Get<TModel>("name")`, every registered collection of the model named
by its element type. It is null when the pinned version is a recorded revert — there is no
document, the rule ran its compiled default — and `CSharpWarnings` lists what the print could not
make exact: a higher-order rule's collection selector, an object payload, an expression leaf.

## What Comes Next

Studio's own screen over a reproduction — the model as a scenario row, Live pinned to the logged
version, the fidelity as a chip, "save as scenario" — is a later design over these same endpoints.
