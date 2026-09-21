---
title: The MCP Server
description: AddMcp() and MapMotivMcp() — the Model Context Protocol server a coding agent reaches the decision log, the reproducer, the printer, the rule documents and the scenarios through, hosted in the adopter's app under the same grants as the rules API.
---

A coding agent debugging a runtime rule needs the same things a person does: the decision that
went wrong, that decision run again under the documents that decided it, the rule as code, and
the samples the rule is meant to satisfy. `AddMcp()` and `MapMotivMcp()` put those behind a
[Model Context Protocol](https://modelcontextprotocol.io) server in the adopter's own host — where
the compiled specs, the decision sink and the resolver already live — over Streamable HTTP, on the
official `ModelContextProtocol.AspNetCore` package.

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddRuleStore(store)
    .AddPropositions(propositionStore)
    .AddScenarios(scenarioStore)
    .AddDecisionLog(sink, log => { /* capture and resolve */ })
    .AddDecisionSource(sink)
    .AddMcp();

app.MapMotivRules("/api/rules");
app.MapMotivMcp("/mcp");          // RequireAuthorization() unless MapMotivMcp("/mcp", anonymous: true)
```

Opt-in: a host that never calls the pair exposes nothing. The server is stateless, so it needs no
sticky sessions behind a load balancer.

## The Tools

| Tool | Input | Returns | Grant |
|---|---|---|---|
| `get_decision` | `id` | the record: rule and version, build, proposition versions, the captured input, the outcome with its justification | `Read` on the rule |
| `list_decisions` | `ruleName?`, `satisfied?`, `fromUtc?`, `toUtc?`, `limit?` (20, at most 200) | records newest first, only for rules the caller may read; `limit` counts records before that filter, so pass `ruleName` to reach a readable rule's older decisions | `Read`, per record |
| `reproduce_decision` | `id` | the [reproduction](./replay.md): pinned rows, the model as far as it could be recovered, the replay, the fidelity verdict, and the rule as C# | `Read` on the rule |
| `print_rule` | `name`, `version?` | a rule or an authored proposition as [C#](../live-rules/csharp-printer.md), with its warnings | `Read` |
| `get_rule` | `name`, `version?` | the document and its version-log row (the live document when the host keeps no log) | `Read` |
| `list_scenarios` | `rule` | the rule's stored [scenarios](../live-rules/AspNetCore.md#scenarios) | `Read` |
| `save_scenario` | `rule`, `name`, `model`, `expectedSatisfied?`, `sourceDecisionId?` | the stored row | `Author` |

Results are structured content serialised with the host's `JsonSerializerOptions`, so they read
exactly as the HTTP surface does.

## Three Rules the Tools Keep

**A rule the caller may not read is not found.** `print_rule` for `billing.vat` without `Read` on
`billing` answers with the very message `print_rule` for `billing.nonexistent` answers with, and
`get_decision` for a decision of such a rule answers as for a purged one. A tool result never
says which rules a caller cannot see.

**A model crosses the wire only as the resolver's result or the log's captured input.** A `Whole`
or `Redacted` capture is the host's JSON of what was captured; a `Reference` capture is the key and
nothing else, whether or not the reproduction managed to resolve it. There is no tool that fetches
a model by key.

**`save_scenario` is the only write.** Its description says so, and that it writes test data, never
behaviour, and is not governed by the approval gate. Everything else is read-only and idempotent,
and says so in its annotations.

## What an Agent Does With Them

1. `get_decision` or `list_decisions` — find the decision a person pointed at.
2. `reproduce_decision` — read `fidelity` first. `isExact` means every anchor was honoured; otherwise
   each note names what slipped. `model.kind == "Reference"` means the model could not be resolved:
   scaffold it from `model.key`, never invent field values.
3. `print_rule` — the rule as code, when the decision is what the rule should keep doing.
4. `list_scenarios` and `save_scenario` — the samples a generated theory runs over, and the
   reproduced decision saved as one more.
5. Write the test with [`RuleSnapshot`](./snapshots.md) as its only dependency.

The tool descriptions carry the fidelity vocabulary and these instructions, so an agent that reads
its tools needs no further prompt.
