# Spec 4G — The First Publish — Design

**Date:** 2026-08-31 (the slice); this document written 2026-08-31
**Status:** Shipped
**Source:** bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§6 step 7 — the last machinery item it names — discharging tickets
[06](https://github.com/karlssberg/Motiv/issues/106) and
[22](https://github.com/karlssberg/Motiv/issues/122). Tracked as
[#160](https://github.com/karlssberg/Motiv/issues/160); shipped as
[#161](https://github.com/karlssberg/Motiv/pull/161).

> **Written after the merge**, per the [#169](https://github.com/karlssberg/Motiv/issues/169) docs
> backlog. Recovered from the shipped diff, its one follow-up commit, and the PR's review and CI
> rounds.
>
> **Ticket #160 states all three defects with reproductions** and names what to build. It is not
> repeated here. What follows is what the ticket and the spec did *not* decide.

## Summary

The spec's step 7 says "publish". By the time this slice opened, the rename had long landed and both
preconditions were discharged (#147, #149), so what was actually missing was **a pipeline** — nothing
in the repository shipped anything to npm at all.

The slice's real finding came from a question the ticket asked first and this design keeps as its
organising idea: **every check in this repository sees the workspace, and a publish ships a tarball.**
Those are different artefacts, and the differences are not incidental — they are exactly the three
places the packages were broken:

| in the workspace | in the tarball |
|---|---|
| `rules-react` reaches `rules-core` through a **symlink** | it reaches it through a **version range** |
| TypeScript resolves both through `paths` and their **`src`** | a consumer resolves through the **`exports` map** |
| **nothing is copied**, so `files` decides nothing | `files` decides what exists |

All three are read for the first time by a consumer, on the registry, *after* the version is
immutable. So the deliverable is not really the release workflow — it is the gate that makes the
workflow safe to fire.

## Decisions (locked)

### 1. Declarations are named per condition, in the shape that keeps the diff honest

Defect 1's fix has two conventional spellings. The one taken nests a `types` **object** inside each
entry, keyed by condition:

```jsonc
".": {
  "types": { "import": "./dist/index.d.ts", "require": "./dist/index.d.cts" },
  "import": "./dist/index.js",
  "require": "./dist/index.cjs"
}
```

The alternative inverts the nesting — `"import": { "types": …, "default": … }`. Both resolve
correctly under `node16`. The nested-`types` form was chosen because it leaves the `import` and
`require` value keys exactly where they were: the diff then reads as *"the one field that was wrong
became two"*, rather than as a rewrite of a map that was otherwise fine.

Applied to all four entry points — two packages × (`.`, `./workflow`) — which is also the count of
`TS1479` errors the pre-fix tree produced.

### 2. The gate **derives** its publishable set; the release train **lists** its manifests

These look inconsistent and are not.

`verify-publishable.mjs` reads `pnpm-workspace.yaml`'s globs and subtracts everything marked
`private`. Nothing is listed, so a package added later is gated **by existing**, and a package made
public by deleting one `private: true` line cannot slip past by not being on a list.

`release-npm.yml`'s tag check names `packages/rules-core/package.json` and
`packages/rules-react/package.json` outright. That is deliberate: the tag asserts a version for the
packages that *release*, and a derived set would silently widen what a tag is taken to promise the
moment someone adds a workspace member.

**Spec 4I settled the argument in the gate's favour.** It added `ui/examples/vue-adapter` as a
workspace member with `examples/*` appended to the globs — and the gate needed **no edit at all**,
because the adapter is `private: true`. The train's explicit list needed none either, which is why
`CLAUDE.md` can state flatly that the second adapter must never be added to it.

### 3. The tag is the authority on the version but does not set it

The job **refuses** if `motiv-rules-v<x>` and the two manifests disagree, rather than stamping the
version into `package.json` during the build.

Stamping is the more common choice and was rejected on a specific ground: npm needs a version in
`package.json` regardless, so stamping would only mean **the committed manifests were a lie between
releases**. Asserting instead keeps `git show <tag>` an honest record of what shipped. The cost is a
version-bump commit before every tag — and that commit is the release's one reviewable artefact,
which is a benefit wearing a cost's clothes.

### 4. `prerelease` is read off the version, never the tag

The NuGet train does `contains(github.ref_name, '-')`, which is correct for `v8.0.0-rc.1`. Copied
here it would mark **every** release a prerelease, because `motiv-rules-v…` carries hyphens in its own
prefix. Caught in review of the workflow's shell logic, before the workflow had ever fired; the
version is stripped from the ref first, then tested.

This is the concrete cost of decision 5 in ticket 06's two-trains form: the trains are separate, so
their conventions cannot simply be copied across.

### 5. Two packers, on purpose

`verify-publishable.mjs` uses `pnpm pack`; `rules-core`'s `isolated-consumer.mjs` keeps `npm pack`.

The isolated consumer's comment argues its choice is safe *because core has no dependencies*, and
names `test/framework-free.test.ts` as what keeps that true. That argument is sound **and it excludes
`rules-react` by construction** — which is the package where a `workspace:*` range would have shipped.
Rather than change the isolated consumer (whose `npm pack` buys portability: npm ships with Node), the
gate covers the case the comment excludes, and does it for every publishable package rather than one.

### 6. The resolution check is a two-variable experiment with one variable

Two consumer projects over the *same* extracted tree, whose `package.json` files differ in exactly one
field: `"type": "module"` versus `"commonjs"`. That single field is the whole of what decides, under
`node16`, which condition of the exports map TypeScript reads. Anything else that differed between the
two would make a failure ambiguous.

`skipLibCheck: true` is on. The claim being made is that the entry points **resolve**, not that the
declarations type-check — that is each package's own `typecheck` script, and leaving lib-checking on
would import third-party declaration noise into this verdict.

### 7. Failures are collected, not thrown

One run reports every defect rather than the first. This is what made step 2 of the plan possible at
all: the gate's first run against unfixed manifests had to be a complete inventory of what was wrong across both
packages, not a one-at-a-time crawl.

### 8. Its own CI job

For the same reason `framework-free` has one: what it checks is a property of the **artefact**, and
every other job in `ui.yml` runs against the workspace, where the three defects are invisible by
construction. It builds only the two publishable packages rather than the whole workspace — Studio is
private and packs nothing.

### 9. The publish stays a maintainer act

The train needs an `NPM_TOKEN` secret with publish rights on the scope, and someone to push a tag.
Nothing in the slice puts a version on the registry, and the ticket asked for exactly that boundary.

## The review rounds

### Copilot: two findings, both refuted, one commit anyway

Both landed on `verify-publishable.mjs`, and neither was a defect:

- **`cpSync` could throw because the `@types` parent is not created first.** It cannot: `cpSync` with
  `recursive: true` creates missing parents, verified against the exact shape the call hits — an
  existing `node_modules` with no `@types` in it. The "missing types" case it worried about is also
  handled a line earlier and not by this call: the copy is guarded on a `.find()` having succeeded,
  and with `skipLibCheck` on, the two consumers still type-check without it.
- **`repoRoot` could miscompute on Windows** because it stripped only a trailing POSIX separator. It
  could not: `dirname` ignores a trailing separator on both platforms, so the `.replace()` was doing
  nothing on either.

The second one still produced the follow-up commit. **A no-op that reads as platform-specific string
surgery earns the second look it got** — so both roots now resolve through `URL`, which has no
separators to trim at all, and the misleading line is gone.

This is the same pattern Spec 4E recorded: refute the finding on the evidence, then remove the code
that invited it. Refuting a bot is not the same as leaving the code alone.

### CI: a red job that was not this PR's

`build` went red on `43aa3b4` with one .NET test failing —
`DecisionLogTelemetryTests.Should_report_how_much_of_the_crash_loss_window_is_occupied` — on a PR
whose diff at that point was a **single JavaScript file**.

The slice did not call it a flake and re-run. It root-caused it: `QueueDepth` is sampled twice, once
by the gauge inside `harness.Collect()` and once by the test on the next line, and the assertion
requires the two samples to agree. With `MaxBatchSize` at 1 and the sink gated closed, the background
writer pulls exactly one record and parks — so the depth is 3 before that pull and 2 after, and
nothing sequences the pull against either read. The sibling drop test is immune only because
`DroppedCount` cannot move backwards.

Filed as [#162](https://github.com/karlssberg/Motiv/issues/162) with the diagnosis and a patch, and
**not fixed here** — an npm-publishing PR has no business touching a C# telemetry race, and the fix
belongs where the rest of that file can be checked for the same shape. **#162 is still open.**

## What this does not do

- **It does not publish.** No version exists on the registry, and none can until a maintainer adds the
  secret and pushes a tag.
- **It does not verify `--provenance`.** That needs the OIDC token a tag build gets and a local run
  does not. It fails loudly rather than silently if the setup is wrong, which is the most that can be
  arranged before a real release.
- **It does not check what the packages *do*.** The gate asks only whether the artefact is shaped like
  something a consumer can install. Behaviour out of a tarball is `verify:isolated`; declaration
  correctness is each package's `typecheck`.
- **It does not gate accessibility on a release.** The a11y suite is deliberately absent from
  `release-npm.yml`: it gates Motiv.Studio, which is private and ships nothing.
- **It does not fix #162**, and did not re-run CI green to hide it.
- **It adds no unit tests.** The gate is a CI script, not a `vitest` case, because it shells out to
  `pnpm pack` and `tsc` over a scratch tree; the discipline it replaces TDD with is the break-every-
  check verification recorded below.

## Verification obligations

Every check was shown to fire, not merely shown to pass:

| check | how it was broken |
|---|---|
| CJS type resolution | failed for real on all four entry points before the fix (`TS1479` ×4) |
| `workspace:` range survived packing | swapping the packer to `npm pack` reports `dependencies.@motiv-rules/core is still "workspace:*"` |
| advertised files exist | pointing an export at `index-missing.cjs` reports it |
| `repository.directory` resolves in this checkout | mistyping it to `rules-cores` reports it |
| one version across publishable packages | bumping react to `0.2.0` reports the disagreement |
| public access, LICENSE, repository, homepage, bugs | all five failed on both packages before the fix |

The release workflow's shell logic was exercised locally against the real manifests: a matching tag
passes and writes both outputs, `motiv-rules-v9.9.9` fails with the manifest-mismatch error, and
`0.2.0-rc.1` sets `prerelease=true`.

`pnpm -r publish --access public --no-git-checks --dry-run` reports **public access** for both
packages, core before react, with `apps/studio` correctly skipped.

Self-review tightened two checks that were weaker than they looked: `entryPoints` mishandled a string
`exports` — which names the whole package at its root, i.e. one entry point — and the repository
check hard-coded
`karlssberg/Motiv` — vacuous in a fork — and now validates `repository.directory` against the
checkout instead.

## Outcome (recorded after the build)

Twelve files, +564/−16, no production TypeScript and no C#.

- **1,119 UI tests green** — rules-core 684, rules-react 28, studio 407 — with `pnpm -r build`,
  `pnpm -r typecheck`, `verify:isolated` and `verify:publishable` all clean.
- `@motiv-rules/core` and `@motiv-rules/react` are importable from CommonJS for the first time.
- The `publishable` job runs on every push, and again on the tagged commit before anything reaches
  the registry.
- The `Publishing status` section of `docs/adoption/index.md` became `Publishing`, and now says what
  the two trains are, how a release is cut, and what a publish is checked against. Nothing linked to
  the old anchor.

### Where later slices moved this

- **Spec 4I** added `examples/*` to the workspace globs for the Vue adapter. The gate absorbed a new
  workspace member with no change to itself — decision 2, paid off. `CLAUDE.md` now records the
  standing rule that the adapter must stay off both the release train and `release-npm.yml`'s
  manifest list, and that `verify:publishable` is what enforces the boundary.
- `docs/adoption/index.md` grew the second adapter's price table further up the page. This slice's
  `Publishing` section is the last one in the file and is unchanged since the merge.
