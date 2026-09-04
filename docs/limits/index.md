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

### The decorator ceiling is reachable from a stored catalogue

Worth stating plainly, because it is the depth the caps below do *not* count. A rule document's every
node that carries a `name` or a `whenTrue` binds to a decorator, and an authored proposition may
reference another authored proposition &mdash; so a catalogue of propositions each referencing the one
before it composes exactly the alternating shape, one link at a time.

None of the three document caps sees it. `MaxDocumentDepth` bounds one document's JSON nesting, and
such a link nests two levels. `MaxNodeCount` bounds one document's nodes, and such a link has three.
`MaxCompositionDepth` bounds the composed depth of one document and stops at a `spec` leaf, so a link
scores 1 however deep the proposition it references happens to be. A chain of 200 links is accepted
with `MaxCompositionDepth` set to 1.

Until a cap counts across references ([#201](https://github.com/karlssberg/Motiv/issues/201)),
**treat reference-chain depth as something your authoring surface must bound**: an application that lets untrusted authors publish propositions can be walked
past the ceiling a few hundred publishes at a time.

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

It *does* count across **decorator layers**. A decorator between two operator layers is not folded
&mdash; it re-enters the fold &mdash; but the nested fold spends the same budget, so a composition
whose size is spread across layers is refused at the same total its flat equivalent is. Fifty layers
of ten operands is over a thousand nodes and is refused by a limit of 100, as the flat chain of 200
is. That is what a rule document composes, since `RuleBinder` wraps every node carrying a `name` or a
`whenTrue`.

> [!NOTE]
> Until [#204](https://github.com/karlssberg/Motiv/issues/204), the **asynchronous** fold still counts
> per fold rather than per evaluation, so `EvaluateAsync` and `MatchesAsync` admit a decorator-layered
> composition that `Evaluate` and `Matches` refuse. The carrier is the difference: a continuation may
> resume on another thread, so the synchronous fold's thread-static budget is not available to it.
> Refuse the document at the edge if you evaluate untrusted compositions asynchronously.

## The document edge: `RuleSerializerOptions`

`Motiv.Serialization` refuses an oversized rule document before it binds, with an error naming the
document rather than a count. Three caps, each bounding something different:

| Option | Default | Bounds |
|---|---|---|
| `MaxDocumentDepth` | 64 | How deeply the JSON nests. |
| `MaxNodeCount` | 10,000 | How many rule nodes the document contains. |
| `MaxCompositionDepth` | 4,096 | How deep the *composed spec* is &mdash; which is not the same thing. |

The third is the one that catches the attack the other two miss. `RuleBinder` folds an n-ary operator
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
