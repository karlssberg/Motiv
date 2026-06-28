# Tag-Triggered NuGet Publishing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish the `Motiv` NuGet package only when a `v*` version tag is pushed, with the git tag as the single source of truth for the version.

**Architecture:** Adopt MinVer so the version is derived from git tags (no hardcoded version anywhere). Split the single CI/publish workflow into a CI-only workflow (test on PR/main, no publish) and a new tag-triggered release workflow (test → pack → push to NuGet → create GitHub Release). Harden packaging with symbol packages and SourceLink.

**Tech Stack:** .NET (multi-target net8.0/net9.0/netstandard2.0/net10.0), MinVer, GitHub Actions, NuGet, Central Package Management.

## Global Constraints

- **Version source of truth:** the git tag. No `<Version>`/`<AssemblyVersion>`/`<FileVersion>` may remain hardcoded in the repo after Task 1.
- **Tag format:** `v`-prefixed, e.g. `v8.1.0` (`MinVerTagPrefix` = `v`).
- **First real release tag must be `v8.0.0` or higher** (repo was previously hardcoded at `8.0.0`).
- **Central Package Management is in use:** package *versions* go in `Directory.Packages.props` (`<PackageVersion>`); package *references* (no version) go in the `.csproj`.
- **`TreatWarningsAsErrors` is `true`** — any new warning fails the build. Do not introduce a duplicate/redundant SourceLink package (the SDK bundles it); use properties only.
- **Project lives at `src/Motiv/Motiv.csproj`** — never use the stale root `Motiv/Motiv.csproj` path.
- **Runners need SDKs 8.0.x, 9.0.x, and 10.0.x** because the package multi-targets all of these; packing builds every target framework.
- **Docs workflows (`docfx.yml`, `jekyll-gh-pages.yml`) must not be modified** — docs stay on their current triggers.

---

### Task 1: Wire up MinVer (tag-driven version)

**Files:**
- Modify: `Directory.Packages.props` (add MinVer version)
- Modify: `src/Motiv/Motiv.csproj` (add MinVer reference + tag prefix)
- Modify: `Directory.Build.props:18-21` (remove hardcoded version block)

**Interfaces:**
- Produces: a build where `dotnet pack` derives the package version from git tags. On an untagged commit MinVer yields a `0.0.0-alpha.0.N` prerelease; on a commit tagged `vX.Y.Z` it yields `X.Y.Z`. Later tasks (CI and release workflows) rely on this — they pass **no** `-p:Version=` argument.

- [ ] **Step 1: Add the MinVer package version to Central Package Management**

In `Directory.Packages.props`, add this line inside the `<ItemGroup>` under the `<!-- Other packages -->` comment (line 39):

```xml
    <PackageVersion Include="MinVer" Version="6.0.0" />
```

- [ ] **Step 2: Reference MinVer and set the tag prefix in Motiv.csproj**

In `src/Motiv/Motiv.csproj`, add `<MinVerTagPrefix>v</MinVerTagPrefix>` to the first `<PropertyGroup>` (after the `<TargetFrameworks>` line), and add a new `<ItemGroup>` with the MinVer reference. The result for those sections:

```xml
    <PropertyGroup>
        <IsPackable>true</IsPackable>
        <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
        <PackageId>Motiv</PackageId>
        <Title>Motiv</Title>
        <Description>Transform the way you work with boolean logic by forming them from discrete propositions. This enables you to dynamically generate custom output, such as providing explanations about the causes behind a result.</Description>
        <PackageTags>Boolean Logic, Decision Making, Explainability, Visibility, Boolean Blindness, Specification Pattern</PackageTags>
        <PackageReadmeFile>README.md</PackageReadmeFile>
        <PackageIcon>icon.png</PackageIcon>
        <TargetFrameworks>net8.0;net9.0;netstandard2.0;net10.0</TargetFrameworks>
        <MinVerTagPrefix>v</MinVerTagPrefix>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="MinVer" PrivateAssets="all" />
    </ItemGroup>
```

- [ ] **Step 3: Remove the hardcoded version from Directory.Build.props**

In `Directory.Build.props`, delete the version-management block (lines 18-21):

```xml
        <!-- Version management -->
        <Version>8.0.0</Version>
        <AssemblyVersion>8.0.0</AssemblyVersion>
        <FileVersion>8.0.0</FileVersion>
```

The `<PropertyGroup>` should end right after `<PackageLicenseExpression>MIT</PackageLicenseExpression>`.

- [ ] **Step 4: Verify MinVer derives the version (test)**

Run:
```bash
dotnet pack src/Motiv/Motiv.csproj -c Release -o ./nupkgs-verify
```
Expected: build succeeds and produces a file named like `nupkgs-verify/Motiv.0.0.0-alpha.0.*.nupkg` (a prerelease, because no `v*` tag exists yet). Confirm with:
```bash
ls nupkgs-verify/
```
Expected output contains `Motiv.0.0.0-alpha.0.<n>.nupkg`. This proves the version now comes from git, not a hardcoded property.

- [ ] **Step 5: Clean up the verification artifacts**

Run:
```bash
rm -rf nupkgs-verify
```

- [ ] **Step 6: Commit**

```bash
git add Directory.Packages.props src/Motiv/Motiv.csproj Directory.Build.props
git commit -m "build: derive package version from git tags via MinVer

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Harden packaging (symbol packages + SourceLink)

**Files:**
- Modify: `Directory.Build.props` (add packaging properties)

**Interfaces:**
- Produces: `dotnet pack` emits a `.snupkg` symbol package alongside each `.nupkg`, and builds are deterministic with SourceLink enabled. Later tasks rely only on the `.snupkg` existing next to the `.nupkg` (so `dotnet nuget push` pushes both).

- [ ] **Step 1: Add packaging properties to Directory.Build.props**

In `Directory.Build.props`, inside the existing `<PropertyGroup>` (after `<PackageLicenseExpression>MIT</PackageLicenseExpression>`, where the version block used to be), add:

```xml
        <!-- Package debugging: symbol packages + SourceLink (SourceLink is bundled in the .NET 8+ SDK) -->
        <IncludeSymbols>true</IncludeSymbols>
        <SymbolPackageFormat>snupkg</SymbolPackageFormat>
        <PublishRepositoryUrl>true</PublishRepositoryUrl>
        <EmbedUntrackedSources>true</EmbedUntrackedSources>
        <ContinuousIntegrationBuild Condition="'$(GITHUB_ACTIONS)' == 'true'">true</ContinuousIntegrationBuild>
```

- [ ] **Step 2: Verify the symbol package is produced (test)**

Run:
```bash
dotnet pack src/Motiv/Motiv.csproj -c Release -o ./nupkgs-verify
ls nupkgs-verify/
```
Expected: output contains BOTH `Motiv.0.0.0-alpha.0.<n>.nupkg` AND `Motiv.0.0.0-alpha.0.<n>.snupkg`.

- [ ] **Step 3: Clean up verification artifacts**

Run:
```bash
rm -rf nupkgs-verify
```

- [ ] **Step 4: Commit**

```bash
git add Directory.Build.props
git commit -m "build: emit snupkg symbol packages and enable SourceLink

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: Convert CI workflow to test-only (remove publish, fix stale paths)

**Files:**
- Modify: `.github/workflows/dotnet.yml` (full rewrite of the `steps:`)

**Interfaces:**
- Consumes: MinVer-configured build from Task 1, snupkg config from Task 2.
- Produces: a CI workflow that, on PR/push to `main`, restores, tests with coverage, builds the solution for validation, and uploads coverage — but never publishes to NuGet.

- [ ] **Step 1: Rewrite dotnet.yml**

Replace the entire contents of `.github/workflows/dotnet.yml` with:

```yaml
# Continuous integration: build and test on every PR and push to main.
# Publishing to NuGet is handled separately by release.yml (triggered by v* tags).

name: .NET CI

on:
  push:
    branches: [ "main" ]
  pull_request:
    branches: [ "main" ]

jobs:
  build:
    runs-on: windows-latest
    steps:
    - uses: actions/checkout@v4
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: |
          8.0.x
          9.0.x
          10.0.x
    - name: Restore dependencies
      run: dotnet restore
    - name: Test
      run: dotnet test --configuration Release --verbosity normal --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
    - name: Pack (validation only — not published)
      run: dotnet pack src/Motiv/Motiv.csproj --configuration Release --output ./nupkgs
    - name: List NuGet Packages
      run: Get-ChildItem -Path .\nupkgs\ -Recurse -Name
    - name: Upload coverage reports to Codecov
      uses: codecov/codecov-action@v4.0.1
      with:
        token: ${{ secrets.CODECOV_TOKEN }}
```

- [ ] **Step 2: Verify the publish step is gone and paths are correct (test)**

Run:
```bash
grep -n "nuget push\|Publish to NuGet\|Motiv/Motiv.csproj" .github/workflows/dotnet.yml || echo "CLEAN: no publish step and no stale paths"
```
Expected output: `CLEAN: no publish step and no stale paths`

- [ ] **Step 3: Verify the trigger still covers PR and main push (test)**

Run:
```bash
grep -n "branches:\s*\[ \"main\" \]" .github/workflows/dotnet.yml
```
Expected: two matches (one under `push:`, one under `pull_request:`).

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/dotnet.yml
git commit -m "ci: make dotnet workflow test-only and fix stale project paths

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: Add tag-triggered release workflow

**Files:**
- Create: `.github/workflows/release.yml`

**Interfaces:**
- Consumes: MinVer config (Task 1) — the workflow passes no version argument; the version comes from the pushed tag. Snupkg config (Task 2) — `dotnet nuget push` auto-pushes the adjacent `.snupkg`.
- Produces: a workflow that, on push of a `v*` tag, runs the test suite, packs the version-stamped package, pushes to NuGet, and creates a GitHub Release.

- [ ] **Step 1: Create release.yml**

Create `.github/workflows/release.yml` with:

```yaml
# Release: triggered by pushing a v* tag (e.g. v8.1.0).
# The tag is the single source of truth for the version (derived via MinVer).
# Steps: test the tagged commit, pack, push to NuGet, create a GitHub Release.

name: Release

on:
  push:
    tags:
      - 'v*'

permissions:
  contents: write   # required to create the GitHub Release

jobs:
  release:
    runs-on: windows-latest
    steps:
    - name: Checkout (full history for MinVer)
      uses: actions/checkout@v4
      with:
        fetch-depth: 0   # MinVer needs full history + tags to derive the version

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: |
          8.0.x
          9.0.x
          10.0.x

    - name: Restore dependencies
      run: dotnet restore

    - name: Test (release gate)
      run: dotnet test --configuration Release --verbosity normal

    - name: Pack (version stamped from the tag by MinVer)
      run: dotnet pack src/Motiv/Motiv.csproj --configuration Release --output ./nupkgs

    - name: List NuGet Packages
      run: Get-ChildItem -Path .\nupkgs\ -Recurse -Name

    - name: Push to NuGet (pushes .nupkg and adjacent .snupkg)
      run: dotnet nuget push "nupkgs/**/*.nupkg" --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate

    - name: Create GitHub Release
      uses: softprops/action-gh-release@v2
      with:
        generate_release_notes: true
        files: |
          nupkgs/*.nupkg
          nupkgs/*.snupkg
```

- [ ] **Step 2: Verify the trigger and key steps (test)**

Run:
```bash
grep -n "tags:\|fetch-depth: 0\|nuget push\|action-gh-release\|contents: write" .github/workflows/release.yml
```
Expected: matches for the tag trigger, `fetch-depth: 0`, the nuget push step, the release action, and the write permission.

- [ ] **Step 3: Confirm no hardcoded version flows into pack (test)**

Run:
```bash
grep -n "\-p:Version\|/p:Version\|PackageVersion=" .github/workflows/release.yml || echo "CLEAN: version comes only from the tag via MinVer"
```
Expected: `CLEAN: version comes only from the tag via MinVer`

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/release.yml
git commit -m "ci: add tag-triggered NuGet release workflow

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: Document the release procedure

**Files:**
- Modify: `README.md` (add a `## Releasing` section)

**Interfaces:**
- Consumes: the release behaviour established in Tasks 1–4.
- Produces: human-readable instructions for cutting a release.

- [ ] **Step 1: Add a Releasing section to README.md**

Add the following section to `README.md`, immediately before the License section (or at the end of the document if there is no License section):

```markdown
## Releasing

Releases are published to NuGet automatically when a version tag is pushed.
The git tag is the single source of truth for the package version (derived via
[MinVer](https://github.com/adamralph/minver)) — there is no version number to
edit in the repository.

To cut a release:

1. Merge your changes to `main` (CI runs the full test suite and deploys docs).
2. Tag the release commit and push the tag:
   ```bash
   git tag v8.1.0
   git push origin v8.1.0
   ```
3. The `Release` workflow then runs the tests, packs `Motiv` at version `8.1.0`,
   pushes the package (and its symbol package) to NuGet, and creates a GitHub
   Release with auto-generated notes.

Tags must be `v`-prefixed (e.g. `v8.1.0`). The first release tag must be
`v8.0.0` or higher. To validate the pipeline without a real release, push a
prerelease tag such as `v8.0.0-rc.1`.
```

- [ ] **Step 2: Verify the section was added (test)**

Run:
```bash
grep -n "## Releasing" README.md
```
Expected: one match.

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: document the tag-triggered release procedure

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Post-Implementation: real-world verification (manual, after merge)

These cannot be fully verified locally and are the final acceptance check:

1. **CI no longer publishes:** after merging to `main`, confirm the `.NET CI` run has no NuGet push step and the package does not appear on NuGet.org.
2. **Release dry run:** push a prerelease tag (`git tag v8.0.0-rc.1 && git push origin v8.0.0-rc.1`). Confirm the `Release` workflow runs, packs version `8.0.0-rc.1`, pushes to NuGet, and creates a GitHub Release. Unlist/delete the prerelease afterwards if desired.
3. **First real release:** push `v8.0.0` (or higher) and confirm `Motiv.8.0.0` appears on NuGet with a symbol package and working SourceLink.
4. **Secret present:** confirm `NUGET_API_KEY` is still configured in the repository's Actions secrets.

## Self-Review Notes

- **Spec coverage:** versioning via MinVer (Task 1), single source of truth = tag (Task 1, no `-p:Version`), `v` prefix (Task 1), workflow split (Tasks 3–4), release test gate (Task 4), GitHub Release (Task 4), packaging hardening (Task 2), docs unchanged (constraint + not touched), release procedure docs (Task 5). All spec sections mapped.
- **Stale-path fix** (discovered during planning) folded into Task 3 since it's required for a working CI pack step.
- **SourceLink** uses the SDK-bundled support (properties only) rather than an explicit package, to avoid a `TreatWarningsAsErrors` failure from a redundant reference.
```