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
  still costs a stack frame per wrapping layer. That depth comes from how an author nests propositions,
  which is bounded by the catalogue; operator depth comes from how many operands a single expression
  has, which is not.
- It covers **synchronous** evaluation. `AsyncSpecBase`'s operators still recurse.

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
