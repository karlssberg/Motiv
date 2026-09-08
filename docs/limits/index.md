---
title: Structural Limits
description: How deeply a Motiv composition can be evaluated, why depth is no longer a stack question, and the two caps that bound the cost of a single evaluation — MotivLimits.MaxEvaluationSize in the engine and RuleSerializerOptions' caps at a document's edge.
---

A Motiv proposition composes into a tree, and a tree has a depth. This page is about how deep that can
go, what it costs, and which of the two caps refuses what.

## Depth is not a stack question

It used to be. Every walk over a composed result &mdash; `Assertions`, `Justification`, `Reason`,
`RootValues` &mdash; recursed once per level, and so did evaluation itself, so a composition deep enough
would abort the process with a `StackOverflowException` that no `catch` can see.

Neither does any more. Result-tree and description-tree walks are iterative, and so is evaluation:
`Evaluate` and `Matches` fold a chain of `And`, `Or`, `XOr`, `AndAlso`, `OrElse` and `Not` operations
onto the heap rather than onto the thread's stack. A hundred-thousand-operand composition evaluates and
reads back on a 1 MB thread.

Two things are worth knowing about the shape of that guarantee:

- It covers the **logical operators**. A composition that alternates operators with *decorated*
  propositions &mdash; a proposition wrapped in another proposition's `WhenTrue`, wrapped again &mdash;
  still costs stack frames per wrapping layer, because each decorator re-enters the fold rather than
  being folded into it. Measured on a 1 MB thread, that shape returns to a depth of **1,047**
  synchronously and **261** asynchronously; a nest of decorators with no operators between them
  reaches 9,327. Past those the process aborts with a stack overflow no `catch` can see.
- It covers **synchronous** evaluation more deeply than asynchronous. An async state-machine frame is
  far fatter than a call frame, so every ceiling above is about four times lower asynchronously.

### The decorator ceiling is reachable from a stored catalogue &mdash; and now bounded

A rule document's every node that carries a `name` or a `whenTrue` binds to a decorator, and an
authored proposition may reference another authored proposition &mdash; so a catalogue of propositions
each referencing the one before it composes exactly the alternating shape, one link at a time.

Two of the three document caps still do not see that. `MaxDocumentDepth` bounds one document's JSON
nesting, and such a link nests two levels; `MaxNodeCount` bounds one document's nodes, and such a link
has three. Neither counts a chain, and neither is meant to.

`MaxCompositionDepth` used to miss it too &mdash; it stopped at a `spec` leaf, so a link scored 1
however deep the proposition it referenced happened to be, and a chain of 200 links was accepted with
the cap set to 1. Since [#201](https://github.com/karlssberg/Motiv/issues/201) both depth caps are
measured **at bind time, against the source a `spec` leaf resolves through**: a proposition inherits
the depth of what it references, and `MaxDecoratorDepth` bounds the decorator subset of that depth
directly. Under the shipped defaults a reference chain is refused at its 129th link, well short of the
261 the async ceiling sits at.

One thing the caps deliberately do not count is a spec **compiled** into the application and
registered through `SpecRegistry.Register`. Motiv has no stack-safe walk of a finished spec tree, so a
registered entry scores as a leaf. That is the argument the caps were always making, applied where it
is true: a compiled spec's depth is what a developer wrote, bounded by their source, not by what a
request or a publish can ask for.

## The engine's backstop: `MotivLimits.MaxEvaluationSize`

Removing a crash removes the thing that used to cap how much one evaluation could spend. `MotivLimits`
puts an explicit cap back:

```csharp
using Motiv;

// process-wide; set it once at startup
MotivLimits.MaxEvaluationSize = 50_000;
```

It counts **nodes**: one per proposition evaluated and one per operation joining them, so a chain of
`n` propositions is `2n - 1` nodes. An evaluation that exceeds it is abandoned with a `SpecException`
naming the limit. The default is 250,000 &mdash; about 50 MB of retained result for the thinnest
composition there is, which is far above anything an author writes and far below what a request body
should be able to spend.

It applies to `Matches` as well as to `Evaluate`. `Matches` materialises no results, but it walks the
same tree, and a composition one entry point accepts should never be one the other refuses.

Two things it is not:

- **Not a validator.** It fires inside the engine, after binding, and its message can only name a
  count. If you load compositions from rule documents, refuse them at the edge instead &mdash; see
  below.
- **Not a bound on all work.** It counts the logical composition. Work done *inside* a node &mdash; a
  higher-order proposition over a large collection &mdash; is not counted by it.

That last exclusion is **declared, not detected**, and it is worth knowing which side of the line your
code falls on. The engine cannot tell a re-entrant evaluation that is part of the composition from one
that is work inside a node, so the library marks the places it knows about: reaching a higher-order
proposition's decision &mdash; resolving its elements *and* applying the predicate to them, including
one you supplied through `As(...)` &mdash; `Where(spec)`, a `Tap` callback, and everything Motiv's own
telemetry does with a span. Anything else that evaluates a proposition while an evaluation is in flight
**is** counted &mdash; a predicate of your own that evaluates a proposition per item, and a
`WhenTrue`/`WhenFalse` or cause-selecting delegate resolved from a result while another evaluation is
running:

```csharp
// per-item work that IS counted against the enclosing rule
var order = Spec.Build((Order o) => o.Lines.All(line.Matches)).Create("lines ok");

// the same intent, where the per-item work is excluded
var order = Spec.Build(line).AsAllSatisfied().Create("lines ok")
                .ChangeModelTo((Order o) => o.Lines);
```

Prefer the built-in quantifiers with [`ChangeModelTo()`](../operators/ChangeModelTo.md) when the
per-item work should not count against the rule that contains it.

The `As(...)` predicate moved to the excluded side in
[#208](https://github.com/karlssberg/Motiv/issues/208). It is how the node reaches its own answer
&mdash; a quorum whose threshold is itself a proposition is one evaluation *inside* one node, not part
of the composition the node sits in &mdash; and until then the elements were excluded while the answer
they were resolved for was not, which is not a line anyone could have described. Its own evaluation is
still bounded, on its own account; it just costs the composition nothing.

Telemetry is on the excluded side deliberately, and it is the one entry on that list you do not write
yourself: **attaching a listener must not change what an evaluation decides.** Motiv tags a span with
the result's own `Reason` and `Assertions`, which resolves your `WhenTrue`/`WhenFalse` delegates, and
disposing the span runs every `ActivityStopped` callback &mdash; an exporter or audit hook that
evaluates a proposition of its own. Charged, merely subscribing to the `Motiv` source could push a
composition past this limit, and for an evaluation nested inside a running one it would surface at an
unrelated node &mdash; see [#209](https://github.com/karlssberg/Motiv/issues/209). A listener or
delegate whose *own* evaluation is oversized is still refused; it just costs the composition nothing.

It *does* count across **decorator layers**. A decorator between two operator layers is not folded
&mdash; it re-enters the fold &mdash; but the nested fold spends the same budget, so a composition
whose size is spread across layers is refused at the same total its flat equivalent is. Fifty layers
of ten operands is over a thousand nodes and is refused by a limit of 100, as the flat chain of 200
is. That is what a rule document composes, since `RuleBinder` wraps every node carrying a `name` or a
`whenTrue`.

It counts the same way on the **asynchronous** surface, which it did not until
[#204](https://github.com/karlssberg/Motiv/issues/204): `EvaluateAsync` and `MatchesAsync` held a
fold-local count of their own and admitted a decorator-layered composition that `Evaluate` and
`Matches` refused. The carrier is what differs and why it took a second change &mdash; a continuation
may resume on a thread whose slot holds a *suspended* evaluation's count, so the synchronous fold's
thread-static is not merely unavailable to an asynchronous evaluation but wrong for it. The count
flows with the evaluation instead, which settles the two shapes the synchronous surface does not have:

- **A concurrent operator** &mdash; `AndConcurrently` and its siblings &mdash; is a fan-out rather than
  a walk, and both branches count against the one budget. A composition of two ten-thousand-node
  branches is twenty thousand nodes, not ten.
- **A synchronous proposition inside an asynchronous composition**, reached through `ToAsyncSpec()`,
  counts against the asynchronous evaluation that contains it. Moving half a composition behind an
  adapter does not buy it a second allowance.

## The document edge: `RuleSerializerOptions`

`Motiv.Serialization` refuses an oversized rule document before it binds, with an error naming the
document rather than a count. Three caps, each bounding something different:

| Option | Default | Bounds |
|---|---|---|
| `MaxDocumentDepth` | 64 | How deeply the JSON nests. |
| `MaxNodeCount` | 10,000 | How many rule nodes the document contains. |
| `MaxCompositionDepth` | 4,096 | How deep the *composed spec* is &mdash; which is not the same thing. |
| `MaxDecoratorDepth` | 128 | How many of those levels are decorators &mdash; the stack half. |

The third is the one that catches the attack the first two miss. `RuleBinder` folds an n-ary operator
left-deep, so a single shallow document node with 5,000 operands composes 4,999 levels deep while
nesting only one. Nesting compounds rather than adds: three operands nested three levels compose six
deep, not three.

`MaxCompositionDepth`'s default is a **cost-per-evaluation budget**. A composition 4,096 deep evaluates
in roughly 1.7 ms and retains about a megabyte of result; 16,384 costs 8.5 ms. That is the budget a
published document may demand of every request that evaluates the rule it binds to, so raise it
knowing what each level buys.

```csharp
var options = new RuleSerializerOptions { MaxCompositionDepth = 1_024 };
```

`MaxDecoratorDepth` budgets a different resource, which is why it is a separate number rather than a
lower `MaxCompositionDepth`. Operator levels are folded onto the heap, so their cost is time and
memory. A decorator level is not folded &mdash; it re-enters the fold, one stack frame at a time
&mdash; so its cost is **stack**, the one resource whose exhaustion cannot be caught. Its default of
128 is derived from the measured ceilings above: half of the lower of the two, leaving the rest of a
1 MB request stack to the caller's own frames.

Both are counted across `spec` references, so a proposition inherits the depth of the propositions it
resolves through. Raising either raises what a whole catalogue may compose, not just one document.

```csharp
var options = new RuleSerializerOptions { MaxDecoratorDepth = 64 };
```
