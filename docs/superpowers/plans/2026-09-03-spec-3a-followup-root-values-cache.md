# Spec 3A follow-up — the projection that was not cached, and the quadratic it was blamed for — Plan

**Date:** 2026-09-03
**Status:** Shipped
**Source:** bundle spec [3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
build step 1 — the cache
[#189](https://github.com/karlssberg/Motiv/issues/189)'s `code-simplifier` pass noticed and left out of
scope. Tracked as [#193](https://github.com/karlssberg/Motiv/issues/193).

Not a build-map slice: #193 is a bug ticket spawned by 3A, not a child of the build map
[#169](https://github.com/karlssberg/Motiv/issues/169), so it takes no row in that map's slice table —
the same call #136, #188, #189, #137, #195 and #192 made. It is recorded on #169 under the follow-ups
the shipped slices spawned.

It is also the **last** open ticket in the 3A chain. #192 closed the correctness line; this closes the
cost line that #137 and #195 opened.

## The debt being paid

`BooleanResultBase` has three root projections. Two materialise on the first read and answer from that
array afterwards; one did not:

```csharp
public IEnumerable<string> RootAssertions    => field ??= this.GetRootAssertions().ToArray();
public IEnumerable<string> AllRootAssertions => field ??= this.GetAllRootAssertions().ToArray();

public IEnumerable<TMetadata> RootValues                  // no field — re-walked on every read
{
    get
    {
        ConstructMetadataTiers();
        return this.GetRootValues().ElseIfEmpty(Values);
    }
}
```

The asymmetry had two halves, because the projection was eager and lazy in the wrong places:

- **the fold ran in the property getter**, so a caller who read the property and never enumerated it
  still paid for the whole walk;
- **the de-duplication was an iterator**, so a caller who enumerated *one* read twice paid for that
  twice.

Neither is fixed by fixing the other, which is why the cost case below is a `[Theory]` with those two
rows.

## The decision

Cache it exactly as the siblings do — `field ??= …ToArray()` — and move `ConstructMetadataTiers()`
behind that cache so the projection stops calling into a walk on reads that already have their answer.

The premise is the one the siblings already rest on and that this file states nowhere: **a
`BooleanResultBase` cannot change once evaluated.** Nothing in the ticket doubted it; it is written
down here because a cache is the first thing to break if it ever stops being true.

## The ticket's third question, answered with numbers rather than judgement

#193 offered an alternative — hoist the walk's memo onto `MetadataNode`, beside the `Underlying` and
`Resolved` it already caches — and asked which was worth it. Measuring the walk settles it, and not in
the direction the ticket expected. See the design doc; in short, the walk it proposes to memoise is
**two levels deep**, so there is nothing in it to save.

## Steps

1. **Failing tests first**, in the idiom the two neighbouring cost files established: a hash census
   (`CountingMetadata.GetHashCode`) rather than a clock, because CI runs Windows and a timing
   assertion there is a flake to be re-run rather than a bound to be read.
2. **Watch them fail for the right reason** — 300 hashes where 0 was claimed, on both rows, and the
   reference-identity case failing for `RootValues` while both siblings pass.
3. **Two contract guards that pass before and after**, so the cache cannot buy its speed with them:
   the fall-back-to-`Values` that [#188](https://github.com/karlssberg/Motiv/issues/188) deliberately
   left in place, and the content and order of the answer itself.
4. **Implement**: `field ??= MaterialiseRootValues()`.
5. **Measure what the ticket asserted**, rather than taking it on trust — both the repeat-read cost it
   names and the tier walk's actual shape.
6. **Full solution suite** on every framework, plus a bare `dotnet build` for the `net472` target that
   only CI runs.
7. **`code-simplifier` pass**, per `CLAUDE.md`.

## Verification

- All thirteen test projects green on net10.0; `Motiv.Tests` and `Motiv.Serialization.Tests` also green
  on net8.0 and net9.0 — 22,360 tests in total.
- The three new `RootValues` cases go red against the pre-change file: 300 hashes against 0, twice, and
  a fresh enumerable against itself.
- The two contract guards and both sibling rows of the symmetry theory pass before *and* after, which
  is what makes them guards rather than cover.
- `dotnet build Motiv.slnx` succeeds with no warnings, covering the `net472` target that has no local
  runner.
- net472 is built but not run: no `mono` host on this machine, a standing local limitation rather than
  anything this change introduces. CI runs it.
