# Spec 4G — The First Publish — Implementation Plan

**Design:** [2026-08-31-spec-4g-first-publish-design.md](../specs/2026-08-31-spec-4g-first-publish-design.md)
**Ticket:** [#160](https://github.com/karlssberg/Motiv/issues/160)
**Source:** bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§6 step 7 — *"Rename/publish `@motiv-rules/core` + `@motiv-rules/react` (curate the barrel first —
ticket 06)"* — under tickets [06](https://github.com/karlssberg/Motiv/issues/106) and
[22](https://github.com/karlssberg/Motiv/issues/122)

> Reconstructed from the shipped diff after the merge, per the docs backlog on
> [#169](https://github.com/karlssberg/Motiv/issues/169). Ticket #160 states all three defects with
> reproductions and names what to build; it is not repeated here.

## Global constraints

- **The preconditions were already discharged.** Ticket 22 asked that the first publish *follow*
  ticket 06's barrel curation and ticket 07's promotion, not precede them. #147 and #149 did both, so
  the sequencing question was settled before this slice opened and needed no re-litigating.
- **TDD, in the shape an artefact allows.** There is no unit test for "the tarball is installable".
  The discipline held anyway: **the gate was written first and run against the unfixed manifests**,
  and it reported every defect before a single one was repaired. Red, then green, then each check
  re-broken deliberately to prove it can still go red.
- **A check that cannot fail is not a check.** Every assertion in the gate was verified by breaking
  the thing it asserts — not by watching it pass on a healthy tree.
- **Nothing here publishes anything.** The slice delivers a pipeline and a gate. A version on the
  registry needs an `NPM_TOKEN` secret and a maintainer to push a tag, and neither is in the diff.
- **No C#.** The .NET side is untouched, and no `dotnet test` result is claimed.

## File structure

```
ui/scripts/verify-publishable.mjs         (new — 252 lines: the publish-readiness gate)
ui/package.json                           (the `verify:publishable` script)
.github/workflows/ui.yml                  (+29 — the `publishable` job)
.github/workflows/release-npm.yml         (new — 102 lines: the `motiv-rules-v*` train)
ui/packages/rules-core/package.json       (per-condition types, publishConfig, repository, metadata)
ui/packages/rules-react/package.json      (the same, plus `files` reformatted)
ui/packages/rules-core/LICENSE            (new — MIT, 21 lines)
ui/packages/rules-react/LICENSE           (new — the same)
docs/adoption/index.md                    ("Publishing status" → "Publishing": two trains, cutting a
                                           release, what a publish is checked against)
README.md ; docs/Overview.md ; CLAUDE.md  (follow)
```

Twelve files, +564/−16. No test file, and no production TypeScript.

## Sequence

1. **Look at the tarball before building anything.** `pnpm pack` both packages, extract, and read
   what a consumer would get. This is the step the whole slice turns on — all three defects are
   invisible in the workspace and obvious in the artefact.
2. **Write the gate against the unfixed manifests.** `verify-publishable.mjs`: derive the publishable
   set from the workspace globs minus `private`, pack, extract into a scratch consumer tree, and
   assert. It came back red on both packages across every metadata check at once, which is the
   failing test.
3. **Add the CJS/ESM resolution check** — two consumers over the same extracted tree, differing only
   in `"type"`, type-checked under `node16`. `TS1479` fires on all four entry points.
4. **Fix defect 1**: name declarations per condition in both exports maps (`.d.ts` under `import`,
   `.d.cts` under `require`), all four entry points.
5. **Fix defect 2**: `publishConfig.access: "public"` in both manifests.
6. **Fix defect 3**: the gate uses `pnpm pack`, so a surviving `workspace:` range is now caught. The
   isolated consumer keeps `npm pack` — see the design doc for why two packers is correct.
7. **Ship the cheap gaps**: `repository`/`homepage`/`bugs`/`author`/`keywords`, and the MIT text as an
   actual `LICENSE` file in each package.
8. **Re-break every check** to confirm each still fails: a mistyped export path, an `npm pack` swap, a
   mistyped `repository.directory`, a divergent version, and the five metadata checks.
9. **The train**: `release-npm.yml` on `motiv-rules-v[0-9]*`. Assert the tag against both manifests,
   then build, typecheck, test, isolated consumer and gate on the tagged commit, then
   `pnpm -r publish --provenance`, then a GitHub Release.
10. **Exercise the workflow's shell logic locally** against the real manifests: a matching tag, a
    mismatched one, and a prerelease version.
11. **Its own CI job**, alongside `framework-free`, for the same reason.
12. **Document it** — `docs/adoption/index.md` gains the two trains, the release procedure and what
    the gate checks; `README.md`, `docs/Overview.md` and `CLAUDE.md` follow.

## The follow-up commit

Copilot raised two findings on `verify-publishable.mjs`. **Both were refuted against Node**, and one
of them was still worth a commit: the code it misread was a no-op, and a no-op that looks like
platform-specific string surgery earns the second look it got. Both roots now resolve through `URL`.

## Not run

`Motiv.Tests`, the `src/examples/*.Tests` suites and `Motiv.Studio` were not exercised by this slice —
it changes no C#. The one thing that **cannot** be exercised anywhere before a real release is
`--provenance`, which needs the OIDC token only a tag build gets.
