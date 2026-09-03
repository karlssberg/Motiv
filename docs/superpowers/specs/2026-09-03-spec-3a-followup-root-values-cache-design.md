# Spec 3A follow-up — the projection that was not cached, and the quadratic it was blamed for — Design

**Date:** 2026-09-03
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
build step 1. Ticket [#193](https://github.com/karlssberg/Motiv/issues/193), raised by the
`code-simplifier` pass on [#189](https://github.com/karlssberg/Motiv/issues/189) and deliberately left
out of its scope. Plan: [here](../plans/2026-09-03-spec-3a-followup-root-values-cache.md).

The last open ticket of the 3A chain. #192 closed the correctness line; this closes the cost line that
[#137](https://github.com/karlssberg/Motiv/issues/137) and
[#195](https://github.com/karlssberg/Motiv/issues/195) opened.

## What changed

One property and one helper:

```csharp
public IEnumerable<TMetadata> RootValues => field ??= MaterialiseRootValues();

private TMetadata[] MaterialiseRootValues()
{
    ConstructMetadataTiers();

    return this.GetRootValues().ElseIfEmpty(Values).ToArray();
}
```

`RootValues` now behaves as `RootAssertions` and `AllRootAssertions` always have. `GetRootValues`,
`RootValuesOf`, `MetadataNode` and the fallback are untouched.

## The measurement refutes the ticket's reason, while confirming its ask

#193 justified itself like this:

> #189 made that walk load-bearing: `RootValues` is now the only reader of the tier tree that descends
> `MetadataNode.Branches`, and the tier tree over a fully-causal `And` chain has a quadratic-plus
> number of edges. A consumer reading `RootValues` twice pays that twice.

The first clause is true and the last does not follow from it. Walking the tier tree from the root of a
left-deep `And` chain of `n` distinctly-named operands, counting nodes and branch depth:

| Operands | Nodes the root's walk visits | Max branch depth |
|---|---|---|
| 100 | 101 | 2 |
| 200 | 201 | 2 |
| 400 | 401 | 2 |

**The walk is `n + 1` nodes, two levels deep.** It is not quadratic and it is not deep, so a repeat
read was paying `Θ(n)`, not `Θ(n²)`.

The reason is #136, three tickets earlier. `MetadataNode.Resolve` builds a node's children by mapping
each cause through `UnderlyingMetadataSources`, and since #136 stopped a composition being its own
source, that mapping reaches **the operands** rather than the intervening compositions. The root's
branches are therefore the `n` leaf tiers directly — flat, not nested. The chain's shape lives in the
result tree; the tier tree over it is a fan.

The ticket cited `DeepCompositionTests.Should_read_RootValues_of_a_deep_composition`'s remark —
*"what remains is quadratic and shared with `Values`"* — for its quadratic. That remark is about the
**tiers' own metadata sets**, one per level, which is what #195 then made lazy so that reading only the
root no longer builds the levels beneath it. Transferring it to the walk was the slip.

The timings agree, on this machine, over 20 repeat reads of one warmed result:

| Operands | Before | After |
|---|---|---|
| 100 | 0.60 ms | 0.00 ms |
| 200 | 1.23 ms | 0.00 ms |
| 400 | 2.46 ms | 0.04 ms |

The "before" column doubles exactly as the chain doubles — linear, as the node counts say — and the
"after" column is the array being handed back. These are local numbers for the record, not a bound;
what CI holds is the hash census, which is a count.

**So the ticket asked for the right change for a wrong reason, and the change is still worth making.**
A projection whose two siblings are cached and which is re-derived on every read is a defect at the
level of the surface's consistency, whatever the exponent turns out to be. Recording the exponent
matters because the *next* reader of #193 would otherwise inherit "the tier walk is quadratic" as a
fact, and the tree it describes has not been that shape since #136.

## The ticket's three questions

#193 listed three things to check. All three are settled, and the third the other way.

**Is it safe to cache at all?** Yes, on the premise the siblings already rest on: a
`BooleanResultBase` cannot change once evaluated. That premise is now written down on `RootValues`,
which is the first place in this file it appears — `RootAssertions` and `AllRootAssertions` have relied
on it silently since they were written.

**Must `ConstructMetadataTiers()` run on every read?** No, and it now runs only on the first. It was
already close to free on later reads — `PostOrderFold.Fold` returns at its first line when the root has
a memoised value, which this result does after the first construction — so this is tidiness rather than
saving. It matters for a different reason: a cached projection that still calls into a walk on every
read is precisely the shape the ticket is about, and leaving that in place would have left the next
reader wondering which half was the fix.

**Should the walk's memo be hoisted onto `MetadataNode`, beside `Underlying` and `Resolved`?** No —
declined on the measurement above. The walk it proposes to memoise is two levels deep, so a memo has
nothing to save; all it would add is an array retained per node, and over the tier tree of a chain
those sum to the square of the chain. Caching at the result retains one array per result that was
actually asked. The ticket framed this as a memory-versus-laziness trade to be measured; measuring it
removes the trade, because one side of it buys nothing.

## What the fix does not do, stated plainly

Reading `RootValues` at **every level** of a chain — the spine sweep — is unchanged:

| Operands | Nodes visited across the spine | Before | After |
|---|---|---|---|
| 100 | 5,148 | 3.11 ms | 3.49 ms |
| 200 | 20,298 | 13.56 ms | 13.63 ms |
| 400 | 80,598 | 64.83 ms | 62.16 ms |

That is `n(n+1)/2` nodes, and it is quadratic — but a per-result cache cannot touch it, because each
spine node is read *once*. Nor could the hoisted node memo: level `k`'s answer is `k` values, so
producing all `n` answers is `Θ(n²)` of output whatever is memoised. **The quadratic is in the answers,
not in the walk**, and it is therefore not a defect to be fixed but the cost of the question. No
follow-up ticket is filed for it, and this table is why.

## The laziness this trades away

`RootValues` was documented by its signature as `IEnumerable<TMetadata>` and was, in fact, half-lazy:
the fold ran in the getter, the de-duplication on enumeration. Two consequences of collapsing that:

- **Exceptions now surface on the read rather than on the enumeration.** Nothing in the walk throws
  today — it is a total projection over an evaluated tree — so this is a note for whoever makes it
  partial, not a behaviour change anyone can observe now.
- **The returned array is the cache.** A caller who casts the `IEnumerable<TMetadata>` back to
  `TMetadata[]` and writes to it now corrupts the result. This is not new exposure: it is exactly what
  `RootAssertions`, `AllRootAssertions`, `SubAssertions` and `AllSubAssertions` have always had, and
  the reason the property type is the read-only interface.

`field ??=` is not thread-safe, and two concurrent first reads can each fold and return different
arrays. That is the shape of all five of these members; introducing locking in one of them would make
the file inconsistent without making it safe.

## What the review pass found

The `code-simplifier` pass found a real hole — in the **test**, not the production change.

The cost theory picked its two rows with a two-arm ternary over the `[InlineData]` string:

```csharp
var repeated = repetition == "read the property twice" ? result.RootValues : firstRead;
```

A two-arm ternary has no unmatched case. Edit either literal on one side only and the mistyped row
falls silently through to `firstRead`, which also reads zero hashes — so the theory stays green while
the eager-getter half of the defect stops being covered. That is the half the class remark calls out as
*not* fixed by fixing the other. It is now a `switch` with a throwing default, which is the shape the
same file's `Read` and both neighbours' `Combine` already use.

**This is the gate lesson from 4I in miniature**: a check that reports a property it is not actually
checking is worse than no check, because everything downstream reads its green as evidence. Worth
noting that the defect was in the *cover for* the fix rather than in the fix, which is the easier place
for it to survive a review that only reads the diff's production half.

Three smaller edits followed: the fallback case was rebuilding `OracleHelpers.Leaf` character for
character, which is the duplication that helper was extracted to end; `"RootValues"` was a bare string
where its two siblings used `nameof`, and `nameof(BooleanResultBase<string>.RootValues)` is a legal
compile-time constant that makes all three rows rename-tracked; and a decade-old `evaulated` typo sat
on the summary line directly above the new remark.

Two proposals were declined with their reasons, both about the same constraint: `CountingMetadata`
stays private to its class because the counter is static and sharing it across classes would make the
counts race, and `CountedChain` therefore cannot be shared with its twin next door either, because
hoisting it would hoist the counter. The duplication is downstream of a justified constraint.

The pass also confirmed `MaterialiseRootValues` is well-named rather than colliding with the
`MaterialiseMetadataTiers` → `ConstructMetadataTiers` rename #195 made: that method was renamed because
it *stopped* forcing what it built, and this one exists to force. The remark now says so, because the
pairing reads as an inconsistency to anyone who remembers only the rename.

## Cost

The change adds one array per result that is asked for its root values — `Θ(n)` for a chain of `n`, and
only when read. It removes the walk from every read after the first. On a single read it is a wash: the
`ToArray()` materialises what the de-duplication's `HashSet` had already built.

## Release note

`RootValues` is materialised on first read and answered from that array afterwards, matching
`RootAssertions` and `AllRootAssertions`. Repeat reads, and repeat enumerations of one read, no longer
re-walk the metadata tier tree. No change to what it answers.

## The chain, closed

Seven tickets came out of Spec 3A and each named the next: 3A deferred the parent-versus-child
divergence to #136; #136 deferred the fallback-to-self to #188; #188 declined to sweep `RootValues`'
own fallback on a stated principle and named #189; #189 named #192 for the assertion twin and, through
its review pass, #193 for this; #137 and #195 took the cost line in parallel. **#193 names nothing
further.**

Two of the seven turned on a ticket's own diagnosis being wrong. #192's was half wrong about the
mechanism and the arithmetic caught it; #193's was wrong about the exponent and the node census caught
it. In both, the ticket's *ask* survived its reasoning — which is the argument for measuring a claim
you are about to act on even when you intend to act on it either way.
