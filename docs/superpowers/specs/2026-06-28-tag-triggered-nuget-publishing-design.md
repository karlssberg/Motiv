# Tag-Triggered NuGet Publishing — Design

**Date:** 2026-06-28
**Status:** Approved for planning

## Problem

The current CI workflow (`.github/workflows/dotnet.yml`) publishes the `Motiv`
package to NuGet on **every push to `main`** (`if: github.ref == 'refs/heads/main'`).
This couples every merge to a release, offers no deliberate release gate, and
relies on a version number hardcoded in `Directory.Build.props` (`8.0.0`) that
must be remembered and bumped by hand — with no enforced link between that number
and what actually gets published.

## Goals

1. Publishing to NuGet is triggered **only by pushing a version tag** (`v*`), not
   by merges to `main`.
2. The **git tag is the single source of truth** for the package version — no
   version number is hardcoded anywhere in the repo.
3. Publishing is gated on a green test run of the exact tagged commit.
4. The published package is debugging-friendly (symbol packages + SourceLink).
5. Continuous integration (test + coverage) and documentation deployment continue
   to run on `main` as they do today.

## Non-Goals

- Changing the documentation workflows (`docfx.yml`, `jekyll-gh-pages.yml`) — docs
  continue to deploy on `main` push, unchanged.
- Publishing the analyzer/codefix projects as standalone packages — they remain
  bundled inside the `Motiv` package as analyzers (current behaviour).
- Automating version bumps, changelog generation beyond GitHub's auto-generated
  release notes, or prerelease/RC publishing flows.

## Decisions

| Decision | Choice |
|---|---|
| Version source | **MinVer**, derived from git tags (single source of truth) |
| Tag format | **`v`-prefixed** (e.g. `v8.1.0`) |
| Docs deployment | **Unchanged** — stays on `main` push |
| Release test gate | **Re-run full test suite** on the tagged commit before publishing |
| GitHub Release | **Yes** — auto-created per tag with auto-generated notes |
| Packaging hardening | **Yes** — snupkg symbol packages + SourceLink |

## Architecture

### Version derivation — MinVer

- Add `MinVer` as a `PrivateAssets="all"` package reference to
  `src/Motiv/Motiv.csproj`.
- Configure the tag prefix in `Motiv.csproj`:
  ```xml
  <MinVerTagPrefix>v</MinVerTagPrefix>
  ```
- **Remove** `<Version>`, `<AssemblyVersion>`, and `<FileVersion>` from
  `Directory.Build.props`. MinVer computes `Version`, `PackageVersion`,
  `AssemblyVersion`, `FileVersion`, and `InformationalVersion` from the nearest
  reachable tag.

Behaviour:
- Tag `v8.1.0` on a commit → that commit packs as `Motiv.8.1.0.nupkg`.
- Commits **after** a tag (untagged) → prerelease such as `8.1.1-alpha.0.3`, so
  local builds and CI smoke-packs never collide with a real release.
- Because the version flows from the tag, the first deliberate release tag must be
  `v8.0.0` or higher (the repo is currently hardcoded at `8.0.0`).

MinVer requires **full git history with tags** at build time, so any job that
packs for release must check out with `fetch-depth: 0`.

### Workflow split

**`.github/workflows/dotnet.yml` — Continuous Integration (modified)**
- Triggers unchanged: `push` and `pull_request` on `main`.
- Steps: restore → test (with coverage) → build (netstandard2.0, net8.0) →
  pack (validation smoke-test only) → upload coverage to Codecov.
- **Removed:** the `Publish to NuGet` step. CI never publishes.

**`.github/workflows/release.yml` — Release (new)**
- Trigger:
  ```yaml
  on:
    push:
      tags: ['v*']
  ```
- Permissions: `contents: write` (needed to create the GitHub Release).
- Steps:
  1. `actions/checkout@v4` with `fetch-depth: 0` (MinVer needs history + tags).
  2. `actions/setup-dotnet@v4` (net8.0; multi-target restore covers the rest).
  3. Restore.
  4. **Test** the tagged commit — release is gated on green tests.
  5. Build `Release`.
  6. Pack `src/Motiv/Motiv.csproj` — MinVer stamps the version from the tag;
     produces `.nupkg` + `.snupkg`.
  7. Push to NuGet:
     `dotnet nuget push "**/*.nupkg" --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate`
     (`--skip-duplicate` makes re-runs idempotent).
  8. Create a GitHub Release for the tag with auto-generated notes
     (e.g. `softprops/action-gh-release` or `gh release create`), attaching the
     `.nupkg`/`.snupkg` artifacts.

### Packaging hardening

In `src/Motiv/Motiv.csproj` (or shared via `Directory.Build.props` where it makes
sense):

```xml
<IncludeSymbols>true</IncludeSymbols>
<SymbolPackageFormat>snupkg</SymbolPackageFormat>
<PublishRepositoryUrl>true</PublishRepositoryUrl>
<EmbedUntrackedSources>true</EmbedUntrackedSources>
<ContinuousIntegrationBuild Condition="'$(GITHUB_ACTIONS)' == 'true'">true</ContinuousIntegrationBuild>
```

Add a SourceLink package reference (`Microsoft.SourceLink.GitHub`,
`PrivateAssets="all"`). Result: deterministic builds and consumers can step into
Motiv source while debugging.

## Secrets / configuration

- `NUGET_API_KEY` — already referenced by the existing workflow; reused by
  `release.yml`. No new secret required (verify it is still present in the repo
  settings).
- `CODECOV_TOKEN` — unchanged, stays in CI.
- GitHub Release creation uses the built-in `GITHUB_TOKEN` (no new secret), with
  `permissions: contents: write` on the release job.

## Testing / verification

- **MinVer wiring:** locally run `dotnet pack src/Motiv/Motiv.csproj -c Release`
  on an untagged commit and confirm a prerelease version is produced; the build
  must succeed with `<Version>` removed from `Directory.Build.props`.
- **CI:** confirm `dotnet.yml` still tests and packs but no longer has a publish
  step; a push to `main` must not publish.
- **Release dry run:** push a throwaway prerelease tag (e.g. `v8.0.0-rc.1`) and
  confirm the release workflow packs the matching version, the test gate runs, and
  push uses `--skip-duplicate`. NuGet prerelease tags are safe to validate the
  pipeline end-to-end before a real release.
- **Full suite:** the release test gate runs the same suite as CI
  (~13k tests across `Motiv.Tests`, `Motiv.CodeFix.Tests`, `Motiv.Analyzer.Tests`,
  and the example projects).

## Release procedure (post-implementation, for the README/docs)

1. Merge changes to `main` (CI runs tests + coverage, deploys docs).
2. Tag the release commit: `git tag v8.1.0 && git push origin v8.1.0`.
3. `release.yml` runs: tests → pack (version `8.1.0` from the tag) → push to NuGet
   → create GitHub Release.

## Risks / edge cases

- **MinVer overrides AssemblyVersion/FileVersion** — any code or test asserting a
  specific assembly version would break. (None known; verify during implementation.)
- **Shallow checkout** in any packing job yields a wrong/`0.0.0` version — every
  release pack job must use `fetch-depth: 0`.
- **First tag below current hardcoded version** — tagging `v7.x` would publish a
  lower version than the existing `8.0.0` baseline; first real tag should be
  `v8.0.0`+.
- **Two pages workflows** (`docfx.yml`, `jekyll-gh-pages.yml`) both target GitHub
  Pages; out of scope here but noted as pre-existing potential contention.
