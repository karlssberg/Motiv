# Runtime Propositions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let users author, namespace, persist and derive propositions (specs) from the UI, composed from any spec in scope — compiled or UI-authored — with edits cascading transactionally to everything that references them.

**Architecture:** A `PropositionSet` mirrors the existing `RuleSet`, layered in front of the immutable compiled `SpecRegistry` behind a new internal `ISpecSource` seam. References bind **directly** (no indirection), so the evaluation hot path is untouched; the cost is paid at publish, where a `BindingScope` rebinds the transitive dependent closure in dependency order and rejects the whole edit if any member breaks. Names gain dots as segment separators, and the UI's namespace tree is a pure projection of the name.

**Tech Stack:** .NET 10 (`net8.0;net9.0;net472;net10.0` multi-target), xUnit + Shouldly, ASP.NET Core minimal APIs; pnpm workspace with React 18 + TypeScript, vanilla CSS, Vitest, Playwright.

## Global Constraints

- **Spec source of truth:** `docs/superpowers/specs/2026-07-30-runtime-propositions-design.md`. Read it before starting.
- **TDD is mandatory** (CLAUDE.md): failing test first, confirm it fails for the right reason, minimum code to pass, confirm pass, commit.
- **Composition only.** Reference-site parameter arguments (`{ "spec": "x", "args": {...} }`) are **out of scope** — a separate spec. Do not add an `args` property anywhere.
- **No expression-leaf work.** `RuleBinder.BindExpressionLeaf` keeps returning `ExpressionsNotEnabled`.
- **UI-authored propositions are `string`-metadata only.** Referencing a compiled spec whose `MetadataType != typeof(string)` from a UI proposition is a `MetadataTypeMismatch` error, never a silent fallback.
- **Zero new runtime dependencies** in `ui/` (the demo's stated ethos). No router, no tree library, no CSS framework.
- **Avoid over-DRYing** (CLAUDE.md): explicit code beats clever abstraction with branching logic.
- **No reflection for model dispatch.** Follow the established `ModelBinding` pattern — capture `TModel` behind closures at registration (`src/Motiv.Serialization.AspNetCore/ModelBinding.cs:5-9`).
- **Dependents never get a version bump.** Version tracks the *document*, not the binding.
- **Every rule and proposition in the store binds, at all times.** An edit breaking a dependent is rejected whole.
- **Test commands must pin a runnable TFM.** `net472` cannot run on macOS. Always `-f net10.0`.
- **Shell prelude for every `dotnet` command:**
  ```bash
  export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
  ```
- **Before declaring done:** run the **full solution** suite. The Poker/ECommerce/SmartHome example tests assert on justification strings (CLAUDE.md).
- **Mandatory final step** (CLAUDE.md): spawn a `code-simplifier` agent to review the changed code; apply its improvements and re-run affected tests.
- **Constructor signature changes:** search production *and* test call sites first — tests construct internal types directly via `InternalsVisibleTo` (CLAUDE.md).

---

## File Structure

### `src/Motiv.Serialization/` (core library)

| File | Responsibility |
|---|---|
| `SpecRegistry.cs` | **Modify** — dotted-name grammar; implement `ISpecSource` |
| `ISpecSource.cs` | **Create** — the one-method-plus-collections lookup seam the binders consume |
| `LayeredSpecSource.cs` | **Create** — overlay first, then registry; collections always from the registry |
| `RuleBinder.cs`, `AsyncRuleBinder.cs`, `MetadataRuleBinder.cs`, `AsyncMetadataRuleBinder.cs`, `CollectionBinding.cs` | **Modify** — mechanical `SpecRegistry` → `ISpecSource` parameter swap |
| `RuleSerializer.cs` | **Modify** — hold an `ISpecSource`; keep the public `SpecRegistry` constructor |
| `RuleErrorCode.cs` | **Modify** — add `InvalidSpecName`, `CycleDetected`, `PropositionNameTaken`, `PropositionReferenced` |
| `Propositions/NodeId.cs` | **Create** — `NodeKind`, `NodeId` (avoids rule/proposition name collision in the graph) |
| `Propositions/DependencyGraph.cs` | **Create** — pure: edges, reverse index, cycle detection, topologically ordered closure |
| `Propositions/DocumentReferences.cs` | **Create** — pure: extract outgoing `spec` names from a parsed document |
| `Propositions/IRebindable.cs` | **Create** — `IRebindable` / `IRebindCommit`: the prepare-all-then-commit-all transaction |
| `Propositions/BindingScope.cs` | **Create** — owns the layered source, write lock, graph, participants; runs the transaction |
| `Propositions/PropositionSet.cs` | **Create** — create/update/delete/revert, versioning, quarantine, listings |
| `Propositions/PropositionOverlay.cs` | **Create** — `ISpecSource` over the authored entries |
| `Propositions/PropositionModelBinding.cs` | **Create** — `TModel` captured behind closures (no reflection) |
| `Propositions/PropositionEntry.cs` | **Create** — public listing record + `PropositionOrigin` enum |
| `Propositions/StoredProposition.cs` | **Create** — the persisted record |
| `Propositions/IPropositionStore.cs` | **Create** — seam + `InMemoryPropositionStore` |
| `Propositions/PropositionUpdateResult.cs` | **Create** — outcome value type incl. `BrokenDependents` |
| `Rules/RuleBase.cs`, `Rules/Rule.cs` | **Modify** — participate as `IRebindable`; rebind without bumping version |
| `Rules/RuleSet.cs` | **Modify** — accept a `BindingScope`; enrol rules as participants |

### `src/Motiv.Serialization.AspNetCore/`

| File | Responsibility |
|---|---|
| `PropositionsContracts.cs` | **Create** — wire records for the six proposition endpoints |
| `MotivPropositionEndpoints.cs` | **Create** — the six handlers, kept out of the already-long endpoints file |
| `MotivRulesEndpoints.cs` | **Modify** — catalog becomes layered + origin-tagged and stops being a closed-over constant |
| `MotivRulesOptions.cs` | **Modify** — record generic proposition-model registrations |
| `MotivRulesServiceCollectionExtensions.cs` | **Modify** — build `BindingScope`, `PropositionSet`, wire the store |

### `ui/packages/rules-core/src/`

| File | Responsibility |
|---|---|
| `dsl/lexer.ts` | **Modify** — admit `.` into `WORD_REST` |
| `contracts.ts` | **Modify** — proposition wire types; `origin` on `CatalogEntry` |
| `client.ts` | **Modify** — five proposition methods |
| `namespaceTree.ts` | **Create** — pure `buildNamespaceTree` / `filterTree` |

### `ui/apps/demo/src/`

| File | Responsibility |
|---|---|
| `routing/useHashRoute.ts` | **Create** — ~30-line hash router |
| `App.tsx` | **Modify** — route between the two pages, own the shared store/client |
| `panes/AppBar.tsx` | **Create** — brand + page tabs + breadcrumb + save controls, extracted from `RuleHeader` |
| `panes/RulesPage.tsx` | **Create** — today's body, unchanged behaviour |
| `panes/PropositionsPage.tsx` | **Create** — rail + the same three panes |
| `explorer/PropositionExplorer.tsx` | **Create** — tree rendering, search, filter chips, node actions |
| `explorer/PropositionDialog.tsx` | **Create** — shared New/Derive dialog |
| `explorer/DependentsStrip.tsx` | **Create** — blast radius |
| `styles/app.css` | **Modify** — rail column, tree, badges, dialog |

---

## Phase 1 — Names & the resolution seam

Three tasks, no behaviour change visible to users. Phase 1 ends with a green full suite and a `SpecRegistry` that accepts dotted names.

### Task 1: Dotted-name grammar

**Files:**
- Modify: `src/Motiv.Serialization/SpecRegistry.cs:109-125`
- Modify: `src/Motiv.Serialization/RuleErrorCode.cs`
- Test: `src/Motiv.Serialization.Tests/SpecRegistryTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `SpecRegistry.Register` accepting dotted names; `RuleErrorCode.InvalidSpecName`.

- [ ] **Step 1: Write the failing tests**

Append to `src/Motiv.Serialization.Tests/SpecRegistryTests.cs`:

```csharp
    [Theory]
    [InlineData("is-active")]
    [InlineData("customer.is-active")]
    [InlineData("customer.eligibility.is-active")]
    [InlineData("a.b.c.d.e")]
    [InlineData("customer.order_total")]
    public void Should_accept_a_dotted_name(string name)
    {
        // Arrange
        var registry = new SpecRegistry();

        // Act
        registry.Register(name, IsPositive);

        // Assert
        registry.Find(name).ShouldNotBeNull();
    }

    [Theory]
    [InlineData(".is-active")]
    [InlineData("is-active.")]
    [InlineData("customer..is-active")]
    [InlineData("customer.1st-order")]
    [InlineData("customer.-leading-hyphen")]
    [InlineData(".")]
    public void Should_reject_a_malformed_dotted_name(string name)
    {
        // Arrange
        var registry = new SpecRegistry();

        // Act
        var register = () => registry.Register(name, IsPositive);

        // Assert
        register.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Should_keep_dotted_names_distinct_from_their_namespace()
    {
        // Arrange
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsPositive)
            .Register("order.is-active", IsPositive);

        // Act & Assert — a namespace is not itself a name
        registry.Find("customer.is-active").ShouldNotBeNull();
        registry.Find("order.is-active").ShouldNotBeNull();
        registry.Find("customer").ShouldBeNull();
        registry.Find("is-active").ShouldBeNull();
    }
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~SpecRegistryTests"
```

Expected: FAIL. `Should_accept_a_dotted_name` throws `ArgumentException` for every dotted case (the current `IsIdentifierLike` rejects `.`). The reject-cases and distinctness test already pass — that is fine and expected; they are regression guards.

- [ ] **Step 3: Replace the grammar check**

In `src/Motiv.Serialization/SpecRegistry.cs`, replace the `IsIdentifierLike` method and its `IsAsciiLetter` helper (lines 109-125) with:

```csharp
    /// <summary>
    /// Dot-separated segments, each an ASCII letter followed by ASCII letters, digits, '-' or '_'.
    /// The dots namespace a name for tree presentation; no leading, trailing or doubled dot.
    /// </summary>
    private static bool IsIdentifierLike(string name)
    {
        var segmentStart = true;

        foreach (var character in name)
        {
            if (character == '.')
            {
                // A dot directly after another dot (or at the very start) leaves no segment between them.
                if (segmentStart)
                    return false;
                segmentStart = true;
                continue;
            }

            if (segmentStart && !IsAsciiLetter(character))
                return false;

            if (!IsAsciiLetter(character) && character is not ((>= '0' and <= '9') or '-' or '_'))
                return false;

            segmentStart = false;
        }

        // Still expecting a segment means the name ended on a dot (or was empty).
        return !segmentStart;
    }

    private static bool IsAsciiLetter(char character) =>
        character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');
```

Also update the `Add` guard's message to describe the new grammar. Replace the `throw new ArgumentException` block at `SpecRegistry.cs:96-99` with:

```csharp
            throw new ArgumentException(
                $"The spec name '{name}' is not a valid identifier: names are referenced from rule " +
                "documents and DSL text, so each dot-separated segment must start with an ASCII " +
                "letter and contain only ASCII letters, digits, '-' or '_'.", nameof(name));
```

**Note:** the old code indexed `name[0]` before the loop, which would throw `IndexOutOfRangeException` on an empty string — reachable only if the `IsNullOrWhiteSpace` guard above it were ever removed. The rewritten loop returns `false` for empty input instead, so the two guards no longer depend on each other's ordering.

- [ ] **Step 4: Expose the grammar for reuse**

`PropositionSet` (Task 8) must reject a name by exactly this grammar, and a second copy of the rule would drift. Add a public predicate beside `Find` in `src/Motiv.Serialization/SpecRegistry.cs` and have `Add` use it:

```csharp
    /// <summary>
    /// Whether a name is a legal spec reference: dot-separated segments, each an ASCII letter
    /// followed by ASCII letters, digits, <c>-</c> or <c>_</c>. Exposed so runtime authoring can
    /// reject a name by the same rule documents are bound by, rather than a second copy of it.
    /// </summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><c>true</c> when the name may be registered and referenced.</returns>
    public static bool IsValidName(string name) =>
        !string.IsNullOrWhiteSpace(name) && IsIdentifierLike(name);
```

Then in `Add`, replace the `if (!IsIdentifierLike(name))` guard's condition with `if (!IsValidName(name))`. Keep the preceding `IsNullOrWhiteSpace` guard — it throws a distinct, clearer message for an empty name.

- [ ] **Step 5: Add the error code**

In `src/Motiv.Serialization/RuleErrorCode.cs`, append inside the enum (after `PolicyRequired`, adding a comma to that member):

```csharp
    /// <summary>A proposition name violates the dot-separated identifier grammar.</summary>
    InvalidSpecName,

    /// <summary>Publishing the document would create a reference cycle.</summary>
    CycleDetected,

    /// <summary>A proposition is already authored under the requested name.</summary>
    PropositionNameTaken,

    /// <summary>The proposition cannot be removed because other documents reference it.</summary>
    PropositionReferenced
```

**Deviation from the spec, deliberately:** the spec lists all four as `RuleErrorCode` members, but only
`InvalidSpecName` and `CycleDetected` end up attached to a `RuleError`. "Name taken" and "referenced"
are whole-request outcomes, not faults at a path inside a document, so they are carried by
`PropositionUpdateOutcome` (Task 6) and their own response shapes (Task 13) instead. Add all four
anyway — the two unused ones give a host a stable code to switch on if it wants to surface these as
errors — but expect the `code-simplifier` pass in Task 21 to query them. Keep them, and say why.

- [ ] **Step 6: Run tests to verify they pass**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~SpecRegistryTests"
```

Expected: PASS, all cases.

- [ ] **Step 7: Confirm nothing else depended on the old grammar**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Motiv.Serialization/SpecRegistry.cs src/Motiv.Serialization/RuleErrorCode.cs src/Motiv.Serialization.Tests/SpecRegistryTests.cs
git commit -m "feat(serialization): namespace spec names with dot-separated segments"
```

---

### Task 2: The `ISpecSource` seam

Mechanical, wide, behaviour-preserving. The binders stop naming `SpecRegistry` and take a lookup seam instead, so a layered source can be substituted in Task 3 without the binders learning that propositions exist.

**Files:**
- Create: `src/Motiv.Serialization/ISpecSource.cs`
- Modify: `src/Motiv.Serialization/SpecRegistry.cs` (implement the interface)
- Modify: `src/Motiv.Serialization/RuleBinder.cs`, `AsyncRuleBinder.cs`, `MetadataRuleBinder.cs`, `AsyncMetadataRuleBinder.cs`, `CollectionBinding.cs`
- Modify: `src/Motiv.Serialization/RuleSerializer.cs`
- Test: `src/Motiv.Serialization.Tests/SpecSourceTests.cs` (create)

**Interfaces:**
- Consumes: Task 1's grammar.
- Produces: `internal interface ISpecSource { SpecRegistryEntry? Find(string name); CollectionBinding<TParent>? FindCollection<TParent>(string path); }`, implemented by `SpecRegistry`. Every binder entry point now takes `ISpecSource source` in place of `SpecRegistry registry`.

- [ ] **Step 1: Write the failing test**

Create `src/Motiv.Serialization.Tests/SpecSourceTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests;

public class SpecSourceTests
{
    private static SpecBase<int, string> IsPositive { get; } =
        Spec.Build((int n) => n > 0).Create("is positive");

    [Fact]
    public void Should_expose_a_registry_as_a_spec_source()
    {
        // Arrange
        var registry = new SpecRegistry().Register("is-positive", IsPositive);

        // Act
        ISpecSource source = registry;

        // Assert
        source.Find("is-positive").ShouldNotBeNull();
        source.Find("absent").ShouldBeNull();
    }

    [Fact]
    public void Should_resolve_registered_collections_through_the_source()
    {
        // Arrange
        var registry = new SpecRegistry();
        registry.RegisterCollection<Basket, int>("items", basket => basket.Items);

        // Act
        ISpecSource source = registry;

        // Assert
        source.FindCollection<Basket>("items").ShouldNotBeNull();
        source.FindCollection<Basket>("absent").ShouldBeNull();
    }

    private sealed record Basket(IReadOnlyList<int> Items);
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~SpecSourceTests"
```

Expected: FAIL to compile — `The type or namespace name 'ISpecSource' could not be found`.

- [ ] **Step 3: Create the seam**

Create `src/Motiv.Serialization/ISpecSource.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// Where a binder resolves the names in a rule document. Kept at exactly the shape
/// <see cref="SpecRegistry"/> already offers, so binders resolve a name to a
/// <see cref="SpecRegistryEntry"/> and never learn whether the entry was compiled into the
/// application or authored at runtime.
/// </summary>
/// <remarks>
/// Collections are host-registered in compiled code and have no runtime counterpart, so a layered
/// source resolves <see cref="FindCollection{TParent}"/> straight through to the registry.
/// </remarks>
internal interface ISpecSource
{
    /// <summary>Resolves a spec reference, or null when the name is unknown.</summary>
    SpecRegistryEntry? Find(string name);

    /// <summary>Resolves the collection registered for <typeparamref name="TParent"/> at a path, or null.</summary>
    CollectionBinding<TParent>? FindCollection<TParent>(string path);
}
```

- [ ] **Step 4: Implement it on `SpecRegistry`**

In `src/Motiv.Serialization/SpecRegistry.cs`, change the class declaration (line 13) to:

```csharp
public sealed class SpecRegistry : ISpecSource
```

`Find` is already public with the right signature and satisfies the interface. `FindCollection<TParent>` is already `internal` with the right signature (line 86) — an internal interface member implemented by an internal method needs no change.

- [ ] **Step 5: Run test to verify it passes**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~SpecSourceTests"
```

Expected: PASS.

- [ ] **Step 6: Swap the binder parameters**

Purely mechanical: in each file below, replace the parameter type `SpecRegistry registry` with `ISpecSource source`, rename the identifier at every use site within those methods, and update XML doc references from `SpecRegistry` to `ISpecSource`.

| File | Members to change |
|---|---|
| `RuleBinder.cs` | `Bind` (:7), `BindElement` (:19), `BindNode` (:24), `BindOperator` (:41), `BindSpecLeaf` (:65), `BindComposition` (:118), `BindHigherOrder` (:139) |
| `AsyncRuleBinder.cs` | the members at :7, :19, :43, :67, :129, :150 |
| `MetadataRuleBinder.cs` | primary-constructor parameter (:6) — `MetadataRuleBinder<TMetadata>(ISpecSource source, RuleSerializerOptions options)` |
| `AsyncMetadataRuleBinder.cs` | primary-constructor parameter (:5) — same shape |
| `CollectionBinding.cs` | abstract `BindHigherOrder` (:12-13) and the override (:31-32) |

Two call sites resolve collections and need the identifier updated too: `RuleBinder.cs:141` and `MetadataRuleBinder.cs:144` become `source.FindCollection<TModel>(node.PathText!)`.

**Do not** change `MetadataRuleBinder`/`AsyncMetadataRuleBinder` to access the parameter through a property — C# primary-constructor parameters are captured directly, and renaming the parameter is the whole change.

- [ ] **Step 7: Point `RuleSerializer` at the seam**

In `src/Motiv.Serialization/RuleSerializer.cs`, replace the field and constructor (lines 9-20) with:

```csharp
    private readonly ISpecSource _source;
    private readonly RuleSerializerOptions _options;

    /// <summary>Creates a serializer that resolves spec references against the given registry.</summary>
    /// <param name="registry">The registry used to resolve spec references.</param>
    /// <param name="options">Options controlling validation and loading; defaults are used when omitted.</param>
    public RuleSerializer(SpecRegistry registry, RuleSerializerOptions? options = null)
        : this((ISpecSource)(registry ?? throw new ArgumentNullException(nameof(registry))), options)
    {
    }

    /// <summary>
    /// Creates a serializer over a layered source, so runtime-authored propositions shadow and
    /// extend the compiled registry without the binders distinguishing the two.
    /// </summary>
    internal RuleSerializer(ISpecSource source, RuleSerializerOptions? options = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _options = options ?? new RuleSerializerOptions();
    }
```

Then replace every remaining `_registry` with `_source` (four binder call sites: lines 74, 122, 173, 227, plus the `Validate`-adjacent site at 246).

```bash
grep -n "_registry" src/Motiv.Serialization/RuleSerializer.cs
```

Expected: no output once done.

- [ ] **Step 8: Run the full serialization suite**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0
```

Expected: PASS. This task changes no behaviour, so any failure is a mechanical slip in the swap.

- [ ] **Step 9: Commit**

```bash
git add src/Motiv.Serialization/
git add src/Motiv.Serialization.Tests/SpecSourceTests.cs
git commit -m "refactor(serialization): resolve document names through an ISpecSource seam"
```

---

### Task 3: `LayeredSpecSource`

**Files:**
- Create: `src/Motiv.Serialization/LayeredSpecSource.cs`
- Test: `src/Motiv.Serialization.Tests/LayeredSpecSourceTests.cs` (create)

**Interfaces:**
- Consumes: `ISpecSource` (Task 2).
- Produces: `internal sealed class LayeredSpecSource(ISpecSource overlay, SpecRegistry registry) : ISpecSource` — overlay wins on `Find`; `FindCollection` always delegates to the registry.

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Serialization.Tests/LayeredSpecSourceTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests;

public class LayeredSpecSourceTests
{
    private static SpecBase<int, string> Compiled { get; } = Spec.Build((int n) => n > 0).Create("compiled");
    private static SpecBase<int, string> Authored { get; } = Spec.Build((int n) => n > 100).Create("authored");

    /// <summary>A minimal overlay standing in for the proposition store.</summary>
    private sealed class StubOverlay(params SpecRegistryEntry[] entries) : ISpecSource
    {
        public SpecRegistryEntry? Find(string name) =>
            entries.FirstOrDefault(entry => entry.Name == name);

        public CollectionBinding<TParent>? FindCollection<TParent>(string path) => null;
    }

    private static SpecRegistryEntry Entry(string name, SpecBase<int, string> spec) =>
        new SpecRegistry().Register(name, spec).Find(name)!;

    [Fact]
    public void Should_prefer_the_overlay_over_the_registry()
    {
        // Arrange
        var registry = new SpecRegistry().Register("is-big", Compiled);
        var source = new LayeredSpecSource(new StubOverlay(Entry("is-big", Authored)), registry);

        // Act
        var entry = source.Find("is-big");

        // Assert
        entry.ShouldNotBeNull();
        entry.Spec.ShouldBeSameAs(Authored);
    }

    [Fact]
    public void Should_fall_through_to_the_registry_when_the_overlay_is_empty()
    {
        // Arrange — this is what revert relies on: remove the overlay entry and the compiled spec reappears
        var registry = new SpecRegistry().Register("is-big", Compiled);
        var source = new LayeredSpecSource(new StubOverlay(), registry);

        // Act
        var entry = source.Find("is-big");

        // Assert
        entry.ShouldNotBeNull();
        entry.Spec.ShouldBeSameAs(Compiled);
    }

    [Fact]
    public void Should_report_an_unknown_name_as_null()
    {
        // Arrange
        var source = new LayeredSpecSource(new StubOverlay(), new SpecRegistry());

        // Act & Assert
        source.Find("absent").ShouldBeNull();
    }

    [Fact]
    public void Should_resolve_collections_from_the_registry_only()
    {
        // Arrange — collections are compiled-only, so the overlay must not be consulted
        var registry = new SpecRegistry();
        registry.RegisterCollection<Basket, int>("items", basket => basket.Items);
        var source = new LayeredSpecSource(new StubOverlay(), registry);

        // Act & Assert
        source.FindCollection<Basket>("items").ShouldNotBeNull();
    }

    private sealed record Basket(IReadOnlyList<int> Items);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~LayeredSpecSourceTests"
```

Expected: FAIL to compile — `LayeredSpecSource` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/Motiv.Serialization/LayeredSpecSource.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// Resolves names against runtime-authored propositions first, then the compiled registry.
/// </summary>
/// <remarks>
/// The layering *is* the override mechanism, and it is what makes revert free: an authored
/// proposition that is removed from the overlay stops shadowing, and the compiled entry — which was
/// never copied or moved — resolves again. It also keeps <see cref="SpecRegistry"/> an honest record
/// of what the developer compiled in, which a mutable registry could not be.
/// </remarks>
internal sealed class LayeredSpecSource(ISpecSource overlay, SpecRegistry registry) : ISpecSource
{
    public SpecRegistryEntry? Find(string name) => overlay.Find(name) ?? registry.Find(name);

    // Collections are registered in compiled code and have no runtime counterpart.
    public CollectionBinding<TParent>? FindCollection<TParent>(string path) =>
        registry.FindCollection<TParent>(path);
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~LayeredSpecSourceTests"
```

Expected: PASS, four tests.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/LayeredSpecSource.cs src/Motiv.Serialization.Tests/LayeredSpecSourceTests.cs
git commit -m "feat(serialization): layer authored propositions over the compiled registry"
```

---

## Phase 2 — The dependency graph and the publish transaction

Tasks 4 and 5 are pure and fully unit-testable with no I/O and no binding. Tasks 6–8 assemble them into the store.

### Task 4: Extract a document's outgoing references

**Files:**
- Create: `src/Motiv.Serialization/Propositions/DocumentReferences.cs`
- Test: `src/Motiv.Serialization.Tests/Propositions/DocumentReferencesTests.cs` (create; create the directory)

**Interfaces:**
- Consumes: internal `RuleDocument` / `RuleNode` / `RuleOperator`.
- Produces: `internal static class DocumentReferences` with
  `public static IReadOnlyList<string> From(RuleDocument document)` — distinct `spec` names in document order.

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Serialization.Tests/Propositions/DocumentReferencesTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class DocumentReferencesTests
{
    private static IReadOnlyList<string> ReferencesOf(string json)
    {
        var errors = new List<RuleError>();
        var document = new RuleDocumentParser(new RuleSerializerOptions()).Parse(json, errors);
        errors.ShouldBeEmpty();
        return DocumentReferences.From(document!);
    }

    [Fact]
    public void Should_find_the_reference_in_a_single_leaf()
    {
        // Act
        var references = ReferencesOf("""{ "spec": "is-active" }""");

        // Assert
        references.ShouldBe(["is-active"]);
    }

    [Fact]
    public void Should_find_references_across_a_composition()
    {
        // Act
        var references = ReferencesOf(
            """{ "and": [ { "spec": "customer.is-active" }, { "spec": "customer.is-adult" } ] }""");

        // Assert
        references.ShouldBe(["customer.is-active", "customer.is-adult"]);
    }

    [Fact]
    public void Should_find_references_beneath_a_negation()
    {
        // Act
        var references = ReferencesOf("""{ "not": { "spec": "is-active" } }""");

        // Assert
        references.ShouldBe(["is-active"]);
    }

    [Fact]
    public void Should_find_the_reference_inside_a_higher_order_subtree()
    {
        // Act — the quantified child is a real edge: editing is-large-order changes this document's meaning
        var references = ReferencesOf(
            """{ "asAllSatisfied": { "path": "orders", "rule": { "spec": "is-large-order" } } }""");

        // Assert
        references.ShouldBe(["is-large-order"]);
    }

    [Fact]
    public void Should_report_each_name_once_even_when_referenced_twice()
    {
        // Arrange — the graph needs a set of edges, not a bag
        var json = """
            { "or": [ { "spec": "is-active" }, { "and": [ { "spec": "is-active" }, { "spec": "is-adult" } ] } ] }
            """;

        // Act
        var references = ReferencesOf(json);

        // Assert
        references.ShouldBe(["is-active", "is-adult"]);
    }

    [Fact]
    public void Should_report_no_references_for_a_document_with_no_spec_leaves()
    {
        // Act
        var references = ReferencesOf("""{ "expression": "n > 0" }""");

        // Assert
        references.ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~DocumentReferencesTests"
```

Expected: FAIL to compile — `DocumentReferences` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/Motiv.Serialization/Propositions/DocumentReferences.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// Reads the spec names a document references. These are the outgoing edges of the dependency
/// graph: the set of propositions whose republication changes this document's meaning.
/// </summary>
internal static class DocumentReferences
{
    /// <summary>The distinct spec names the document references, in document order.</summary>
    public static IReadOnlyList<string> From(RuleDocument document)
    {
        if (document.Root is null)
            return [];

        // Ordinal-ordered set: names are an ordinal contract, and callers compare graphs by content.
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Collect(document.Root, names, seen);
        return names;
    }

    private static void Collect(RuleNode node, List<string> names, HashSet<string> seen)
    {
        if (node.Operator == RuleOperator.Spec && node.SpecName is { } name && seen.Add(name))
            names.Add(name);

        foreach (var child in node.Children)
            Collect(child, names, seen);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~DocumentReferencesTests"
```

Expected: PASS, six tests. If the higher-order test fails, check whether the parser nests the quantified rule under `Children` — it does (`RuleNode.Children` is the single child list for every operator), so a failure here means the JSON shape in the test does not match `rule.v1.json`; verify against `schemas/rule.v1.json` rather than changing the production code.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/Propositions/DocumentReferences.cs src/Motiv.Serialization.Tests/Propositions/DocumentReferencesTests.cs
git commit -m "feat(serialization): read a document's outgoing spec references"
```

---

### Task 5: `DependencyGraph` — cycles and topologically ordered closure

The most subtle task in the plan. The closure ordering is load-bearing: rebinding a referrer before its dependency binds it against the *old* definition and reports no error at all. Step 1 includes the negative test that pins this.

**Files:**
- Create: `src/Motiv.Serialization/Propositions/NodeId.cs`
- Create: `src/Motiv.Serialization/Propositions/DependencyGraph.cs`
- Test: `src/Motiv.Serialization.Tests/Propositions/DependencyGraphTests.cs` (create)

**Interfaces:**
- Consumes: nothing (pure).
- Produces:
  ```csharp
  internal enum NodeKind { Proposition, Rule }
  internal readonly record struct NodeId(NodeKind Kind, string Name)
  {
      public static NodeId Proposition(string name);
      public static NodeId Rule(string name);
  }
  internal sealed class DependencyGraph
  {
      public void Set(NodeId node, IReadOnlyList<string> references);
      public void Remove(NodeId node);
      public IReadOnlyList<NodeId> Referrers(string propositionName);
      public IReadOnlyList<NodeId> DependentClosure(string propositionName);
      public IReadOnlyList<string>? FindCycle(string propositionName, IReadOnlyList<string> prospectiveReferences);
  }
  ```
  `DependentClosure` excludes the named proposition itself and is ordered dependencies-before-dependents.

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Serialization.Tests/Propositions/DependencyGraphTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class DependencyGraphTests
{
    [Fact]
    public void Should_report_direct_referrers()
    {
        // Arrange
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);
        graph.Set(NodeId.Proposition("c"), ["a"]);
        graph.Set(NodeId.Proposition("d"), ["b"]);

        // Act
        var referrers = graph.Referrers("a");

        // Assert
        referrers.ShouldBe([NodeId.Proposition("b"), NodeId.Proposition("c")], ignoreOrder: true);
    }

    [Fact]
    public void Should_report_rules_as_referrers_alongside_propositions()
    {
        // Arrange
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);
        graph.Set(NodeId.Rule("can-checkout"), ["a"]);

        // Act
        var referrers = graph.Referrers("a");

        // Assert
        referrers.ShouldBe([NodeId.Proposition("b"), NodeId.Rule("can-checkout")], ignoreOrder: true);
    }

    [Fact]
    public void Should_keep_a_rule_and_a_proposition_of_the_same_name_distinct()
    {
        // Arrange — nothing stops a host naming a rule after a proposition; the graph must not merge them
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("shared"), ["a"]);
        graph.Set(NodeId.Rule("shared"), ["b"]);

        // Act & Assert
        graph.Referrers("a").ShouldBe([NodeId.Proposition("shared")]);
        graph.Referrers("b").ShouldBe([NodeId.Rule("shared")]);
    }

    [Fact]
    public void Should_report_the_transitive_closure_excluding_the_edited_node()
    {
        // Arrange — a <- b <- c, and a <- d
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);
        graph.Set(NodeId.Proposition("c"), ["b"]);
        graph.Set(NodeId.Proposition("d"), ["a"]);

        // Act
        var closure = graph.DependentClosure("a");

        // Assert
        closure.ShouldBe(
            [NodeId.Proposition("b"), NodeId.Proposition("d"), NodeId.Proposition("c")],
            ignoreOrder: true);
        closure.ShouldNotContain(NodeId.Proposition("a"));
    }

    [Fact]
    public void Should_order_the_closure_dependencies_before_dependents()
    {
        // Arrange — a <- b <- c <- d, deliberately registered in reverse so insertion order cannot pass by luck
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("d"), ["c"]);
        graph.Set(NodeId.Proposition("c"), ["b"]);
        graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act
        var closure = graph.DependentClosure("a");

        // Assert
        closure.ShouldBe([NodeId.Proposition("b"), NodeId.Proposition("c"), NodeId.Proposition("d")]);
    }

    [Fact]
    public void Should_order_a_diamond_so_both_sides_precede_the_join()
    {
        // Arrange — a <- b, a <- c, and d references both b and c
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);
        graph.Set(NodeId.Proposition("c"), ["a"]);
        graph.Set(NodeId.Proposition("d"), ["b", "c"]);

        // Act
        var closure = graph.DependentClosure("a");

        // Assert — the join must come last; the order of b and c relative to each other is free
        closure.Count.ShouldBe(3);
        closure[2].ShouldBe(NodeId.Proposition("d"));
        closure.ShouldContain(NodeId.Proposition("b"));
        closure.ShouldContain(NodeId.Proposition("c"));
    }

    /// <summary>
    /// The negative test that pins the ordering. If the topological sort is ever "simplified" to a
    /// plain reverse-BFS, every other cascade test still passes — wrong-order rebinding reports
    /// *fewer* errors, not different ones — so this is the only guard against silent
    /// under-reporting.
    /// </summary>
    [Fact]
    public void Should_never_place_a_dependent_before_something_it_depends_on()
    {
        // Arrange — a chain plus a shortcut edge, so a naive breadth-first order puts 'd' at depth 1
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);
        graph.Set(NodeId.Proposition("c"), ["b"]);
        graph.Set(NodeId.Proposition("d"), ["a", "c"]);

        // Act
        var closure = graph.DependentClosure("a");

        // Assert — 'd' depends on 'c', so it must follow it despite also referencing 'a' directly
        closure.IndexOf(NodeId.Proposition("c"))
            .ShouldBeLessThan(closure.IndexOf(NodeId.Proposition("d")));
        closure.IndexOf(NodeId.Proposition("b"))
            .ShouldBeLessThan(closure.IndexOf(NodeId.Proposition("c")));
    }

    [Fact]
    public void Should_detect_a_direct_self_reference()
    {
        // Arrange
        var graph = new DependencyGraph();

        // Act
        var cycle = graph.FindCycle("a", ["a"]);

        // Assert
        cycle.ShouldNotBeNull();
        cycle.ShouldBe(["a", "a"]);
    }

    [Fact]
    public void Should_detect_a_transitive_cycle()
    {
        // Arrange — b references a; giving a a reference to b closes the loop
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act
        var cycle = graph.FindCycle("a", ["b"]);

        // Assert
        cycle.ShouldNotBeNull();
        cycle.ShouldBe(["a", "b", "a"]);
    }

    [Fact]
    public void Should_allow_a_diamond_which_is_not_a_cycle()
    {
        // Arrange — b and c both reference a; d referencing both is a diamond, perfectly legal
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);
        graph.Set(NodeId.Proposition("c"), ["a"]);

        // Act
        var cycle = graph.FindCycle("d", ["b", "c"]);

        // Assert
        cycle.ShouldBeNull();
    }

    [Fact]
    public void Should_ignore_references_to_names_with_no_edges_of_their_own()
    {
        // Arrange — a compiled spec has no outgoing edges and can never close a cycle
        var graph = new DependencyGraph();

        // Act
        var cycle = graph.FindCycle("a", ["compiled-spec"]);

        // Assert
        cycle.ShouldBeNull();
    }

    [Fact]
    public void Should_forget_a_removed_node()
    {
        // Arrange
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act
        graph.Remove(NodeId.Proposition("b"));

        // Assert
        graph.Referrers("a").ShouldBeEmpty();
        graph.DependentClosure("a").ShouldBeEmpty();
    }

    [Fact]
    public void Should_replace_a_nodes_edges_when_set_again()
    {
        // Arrange
        var graph = new DependencyGraph();
        graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act — b is edited to reference c instead of a
        graph.Set(NodeId.Proposition("b"), ["c"]);

        // Assert
        graph.Referrers("a").ShouldBeEmpty();
        graph.Referrers("c").ShouldBe([NodeId.Proposition("b")]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~DependencyGraphTests"
```

Expected: FAIL to compile — `NodeId` and `DependencyGraph` do not exist.

- [ ] **Step 3: Write `NodeId`**

Create `src/Motiv.Serialization/Propositions/NodeId.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>What kind of thing a dependency-graph node is.</summary>
internal enum NodeKind
{
    /// <summary>An authored proposition. Referenceable, so it can be an edge target.</summary>
    Proposition,

    /// <summary>A live rule. Documents reference specs and never rules, so a rule is always a sink.</summary>
    Rule
}

/// <summary>
/// Identifies a node in the dependency graph. Kind is part of the identity because nothing stops a
/// host naming a rule after a proposition, and merging the two would corrupt the closure.
/// </summary>
internal readonly record struct NodeId(NodeKind Kind, string Name)
{
    public static NodeId Proposition(string name) => new(NodeKind.Proposition, name);

    public static NodeId Rule(string name) => new(NodeKind.Rule, name);

    public override string ToString() => $"{Kind}:{Name}";
}
```

- [ ] **Step 4: Write `DependencyGraph`**

Create `src/Motiv.Serialization/Propositions/DependencyGraph.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// Who references whom. Holds one outgoing-edge list per node plus a reverse index, and answers the
/// two questions a publish needs: would this edit close a cycle, and what must be rebound because of
/// it — in an order where every dependency precedes its dependents.
/// </summary>
/// <remarks>
/// Not synchronized: every mutation and query runs under the <see cref="BindingScope"/> write lock.
/// </remarks>
internal sealed class DependencyGraph
{
    private readonly Dictionary<NodeId, IReadOnlyList<string>> _outgoing = [];
    private readonly Dictionary<string, HashSet<NodeId>> _incoming = new(StringComparer.Ordinal);

    /// <summary>Replaces a node's outgoing references, keeping the reverse index consistent.</summary>
    public void Set(NodeId node, IReadOnlyList<string> references)
    {
        Detach(node);
        _outgoing[node] = references;
        foreach (var reference in references)
        {
            if (!_incoming.TryGetValue(reference, out var referrers))
                _incoming[reference] = referrers = [];
            referrers.Add(node);
        }
    }

    /// <summary>Drops a node and every edge leaving it.</summary>
    public void Remove(NodeId node)
    {
        Detach(node);
        _outgoing.Remove(node);
    }

    /// <summary>The nodes referencing the named proposition directly.</summary>
    public IReadOnlyList<NodeId> Referrers(string propositionName) =>
        _incoming.TryGetValue(propositionName, out var referrers) ? [.. referrers] : [];

    /// <summary>
    /// Every node transitively affected by republishing the named proposition, ordered so a node
    /// always follows the nodes it depends on. Excludes the named proposition itself.
    /// </summary>
    public IReadOnlyList<NodeId> DependentClosure(string propositionName)
    {
        // Reachable set first, by walking the reverse index breadth-first.
        var affected = new HashSet<NodeId>();
        var queue = new Queue<string>();
        queue.Enqueue(propositionName);

        while (queue.Count > 0)
        {
            foreach (var referrer in Referrers(queue.Dequeue()))
            {
                if (!affected.Add(referrer))
                    continue;
                // Only propositions are referenceable, so only they can carry the walk further.
                if (referrer.Kind == NodeKind.Proposition)
                    queue.Enqueue(referrer.Name);
            }
        }

        // Then order it. Reachability alone is not enough: a node may reference both the edited
        // proposition and another member of the closure, so breadth-first depth does not imply a
        // safe rebind order. Depth-first post-order over the closure's own edges does.
        var ordered = new List<NodeId>(affected.Count);
        var visited = new HashSet<NodeId>();
        foreach (var node in affected)
            Visit(node, affected, visited, ordered);

        return ordered;
    }

    /// <summary>
    /// The cycle the prospective references would create, as a path starting and ending at
    /// <paramref name="propositionName"/>, or null when they would not.
    /// </summary>
    public IReadOnlyList<string>? FindCycle(string propositionName, IReadOnlyList<string> prospectiveReferences)
    {
        foreach (var reference in prospectiveReferences)
        {
            var path = new List<string> { propositionName };
            if (Reaches(reference, propositionName, path, new HashSet<string>(StringComparer.Ordinal)))
                return path;
        }

        return null;
    }

    private void Detach(NodeId node)
    {
        if (!_outgoing.TryGetValue(node, out var previous))
            return;

        foreach (var reference in previous)
        {
            if (!_incoming.TryGetValue(reference, out var referrers))
                continue;
            referrers.Remove(node);
            if (referrers.Count == 0)
                _incoming.Remove(reference);
        }
    }

    /// <summary>Emits <paramref name="node"/> only after every closure member it depends on.</summary>
    private void Visit(NodeId node, HashSet<NodeId> closure, HashSet<NodeId> visited, List<NodeId> ordered)
    {
        if (!visited.Add(node))
            return;

        if (_outgoing.TryGetValue(node, out var references))
        {
            foreach (var reference in references)
            {
                var dependency = NodeId.Proposition(reference);
                if (closure.Contains(dependency))
                    Visit(dependency, closure, visited, ordered);
            }
        }

        ordered.Add(node);
    }

    /// <summary>Walks forward from <paramml name="from"/> looking for <paramref name="target"/>, recording the path.</summary>
    private bool Reaches(string from, string target, List<string> path, HashSet<string> visited)
    {
        path.Add(from);

        if (from == target)
            return true;

        if (visited.Add(from) && _outgoing.TryGetValue(NodeId.Proposition(from), out var references))
        {
            foreach (var reference in references)
            {
                if (Reaches(reference, target, path, visited))
                    return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }
}
```

**Note on the `Visit` recursion:** it is safe against cycles because `FindCycle` runs *before* any `Set`, so the graph never contains one. The `visited` set additionally bounds it, so a corrupt graph degrades to an incomplete order rather than a stack overflow.

- [ ] **Step 5: Fix the XML-doc typo introduced above**

The `Reaches` doc comment contains `<paramml name="from">`. Change it to `<paramref name="from"/>` — a malformed doc tag is a build warning, and `Directory.Build.props` may promote warnings to errors.

```bash
grep -n "paramml" src/Motiv.Serialization/Propositions/DependencyGraph.cs
```

Expected after fixing: no output.

- [ ] **Step 6: Run tests to verify they pass**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~DependencyGraphTests"
```

Expected: PASS, thirteen tests.

- [ ] **Step 7: Verify the negative test actually bites**

Temporarily replace the ordering block in `DependentClosure` with `return [.. affected];` (a plain reachability result), then:

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~DependencyGraphTests"
```

Expected: `Should_order_the_closure_dependencies_before_dependents`, `Should_order_a_diamond_so_both_sides_precede_the_join`, and `Should_never_place_a_dependent_before_something_it_depends_on` FAIL. **Restore the ordering block** and re-run to green before committing. A test that cannot fail is not protecting anything, and this is the one test the whole cascade design rests on.

- [ ] **Step 8: Commit**

```bash
git add src/Motiv.Serialization/Propositions/NodeId.cs src/Motiv.Serialization/Propositions/DependencyGraph.cs src/Motiv.Serialization.Tests/Propositions/DependencyGraphTests.cs
git commit -m "feat(serialization): track proposition dependencies, cycles and rebind order"
```

---

### Task 6: Value types and the persistence seam

Small and mechanical, but it locks in the vocabulary every later task uses. No cascade logic here.

**Files:**
- Create: `src/Motiv.Serialization/Propositions/StoredProposition.cs`
- Create: `src/Motiv.Serialization/Propositions/IPropositionStore.cs`
- Create: `src/Motiv.Serialization/Propositions/PropositionEntry.cs`
- Create: `src/Motiv.Serialization/Propositions/PropositionUpdateResult.cs`
- Test: `src/Motiv.Serialization.Tests/Propositions/InMemoryPropositionStoreTests.cs` (create)

**Interfaces:**
- Consumes: `RuleError` (existing).
- Produces:
  ```csharp
  public sealed record StoredProposition(string Name, string ModelType, string DocumentJson, int Version, string? Description);
  public interface IPropositionStore { IReadOnlyList<StoredProposition> Load(); void Save(StoredProposition proposition); void Delete(string name); }
  public sealed class InMemoryPropositionStore : IPropositionStore;
  public enum PropositionOrigin { Compiled, Overridden, Authored }
  public sealed record PropositionEntry(string Name, string ModelType, string MetadataType, bool IsAsync, PropositionOrigin Origin, int Version, string? Description, IReadOnlyList<RuleError> Quarantine);
  public sealed record BrokenDependent(string Name, string Kind, IReadOnlyList<RuleError> Errors);
  public sealed record PropositionDependent(string Name, string Kind);
  public enum PropositionUpdateOutcome { Created, Updated, Removed, VersionConflict, Invalid, NotFound, NameTaken, Referenced }
  public sealed class PropositionUpdateResult;  // Outcome, Version, Errors, BrokenDependents, Referrers + static factories
  ```

- [ ] **Step 1: Write the failing test**

Create `src/Motiv.Serialization.Tests/Propositions/InMemoryPropositionStoreTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class InMemoryPropositionStoreTests
{
    private static StoredProposition Stored(string name, int version = 1) =>
        new(name, "customer", $$"""{ "spec": "is-active", "name": "{{name}}" }""", version, null);

    [Fact]
    public void Should_start_empty()
    {
        // Act & Assert
        new InMemoryPropositionStore().Load().ShouldBeEmpty();
    }

    [Fact]
    public void Should_round_trip_a_saved_proposition()
    {
        // Arrange
        var store = new InMemoryPropositionStore();

        // Act
        store.Save(Stored("customer.is-eligible"));

        // Assert
        var loaded = store.Load();
        loaded.Count.ShouldBe(1);
        loaded[0].Name.ShouldBe("customer.is-eligible");
        loaded[0].ModelType.ShouldBe("customer");
        loaded[0].Version.ShouldBe(1);
    }

    [Fact]
    public void Should_replace_a_proposition_saved_under_the_same_name()
    {
        // Arrange
        var store = new InMemoryPropositionStore();
        store.Save(Stored("a", version: 1));

        // Act
        store.Save(Stored("a", version: 2));

        // Assert
        store.Load().Count.ShouldBe(1);
        store.Load()[0].Version.ShouldBe(2);
    }

    [Fact]
    public void Should_delete_by_name()
    {
        // Arrange
        var store = new InMemoryPropositionStore();
        store.Save(Stored("a"));
        store.Save(Stored("b"));

        // Act
        store.Delete("a");

        // Assert
        store.Load().Select(proposition => proposition.Name).ShouldBe(["b"]);
    }

    [Fact]
    public void Should_ignore_deleting_an_absent_name()
    {
        // Arrange
        var store = new InMemoryPropositionStore();

        // Act
        var delete = () => store.Delete("absent");

        // Assert — the store is a dumb sink; the set decides what is legal
        delete.ShouldNotThrow();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~InMemoryPropositionStoreTests"
```

Expected: FAIL to compile — `StoredProposition` and `InMemoryPropositionStore` do not exist.

- [ ] **Step 3: Write `StoredProposition`**

Create `src/Motiv.Serialization/Propositions/StoredProposition.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// One authored proposition as it is persisted. The model type is carried explicitly because it is
/// not part of the document — a rule takes its model from the C# class that declares it, and an
/// authored proposition has no such class.
/// </summary>
/// <param name="Name">The dot-separated name documents reference the proposition by.</param>
/// <param name="ModelType">The registered model-type id the document binds against.</param>
/// <param name="DocumentJson">The rule document defining the proposition.</param>
/// <param name="Version">The document's revision, starting at 1.</param>
/// <param name="Description">An optional human-readable description surfaced in the catalog.</param>
public sealed record StoredProposition(
    string Name, string ModelType, string DocumentJson, int Version, string? Description);
```

- [ ] **Step 4: Write the store seam**

Create `src/Motiv.Serialization/Propositions/IPropositionStore.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// Where authored propositions are kept between restarts. Deliberately narrow and synchronous, to
/// match the synchronous publish path: implementations are called while the publish lock is held, so
/// they must be quick.
/// </summary>
/// <remarks>
/// A store is a dumb sink — it validates nothing and enforces no invariants. Legality is decided by
/// <see cref="PropositionSet"/> before anything reaches here.
/// </remarks>
public interface IPropositionStore
{
    /// <summary>Every persisted proposition, read once at startup.</summary>
    IReadOnlyList<StoredProposition> Load();

    /// <summary>Persists a proposition, replacing any existing one of the same name.</summary>
    void Save(StoredProposition proposition);

    /// <summary>Removes a proposition, doing nothing when the name is absent.</summary>
    void Delete(string name);
}

/// <summary>The default store: propositions live for the lifetime of the process, as rules do.</summary>
public sealed class InMemoryPropositionStore : IPropositionStore
{
    private readonly Dictionary<string, StoredProposition> _propositions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IReadOnlyList<StoredProposition> Load() => [.. _propositions.Values];

    /// <inheritdoc />
    public void Save(StoredProposition proposition) => _propositions[proposition.Name] = proposition;

    /// <inheritdoc />
    public void Delete(string name) => _propositions.Remove(name);
}
```

- [ ] **Step 5: Write the listing types**

Create `src/Motiv.Serialization/Propositions/PropositionEntry.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>Where a proposition's current definition comes from.</summary>
public enum PropositionOrigin
{
    /// <summary>Compiled into the application; no authored document shadows it.</summary>
    Compiled,

    /// <summary>Compiled into the application, with an authored document currently shadowing it.</summary>
    Overridden,

    /// <summary>Authored at runtime, with no compiled counterpart.</summary>
    Authored
}

/// <summary>
/// One proposition as listed to a client: the effective definition plus where it came from.
/// </summary>
/// <param name="Name">The dot-separated name.</param>
/// <param name="ModelType">The registered model-type id, or the CLR type name when not registered.</param>
/// <param name="MetadataType">The metadata type name (e.g. String).</param>
/// <param name="IsAsync">Whether the effective definition evaluates asynchronously.</param>
/// <param name="Origin">Whether the definition is compiled, overridden, or authored.</param>
/// <param name="Version">The authored document's version, or 0 for a purely compiled proposition.</param>
/// <param name="Description">An optional human-readable description.</param>
/// <param name="Quarantine">
/// The binding errors that excluded an authored document from the effective set, or empty when it
/// bound. Quarantine is orthogonal to <see cref="Origin"/>, not a fourth value of it: an overridden
/// or an authored proposition can each be quarantined.
/// </param>
public sealed record PropositionEntry(
    string Name,
    string ModelType,
    string MetadataType,
    bool IsAsync,
    PropositionOrigin Origin,
    int Version,
    string? Description,
    IReadOnlyList<RuleError> Quarantine);
```

- [ ] **Step 6: Write the outcome type**

Create `src/Motiv.Serialization/Propositions/PropositionUpdateResult.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>A dependent that the attempted edit would have stopped binding.</summary>
/// <param name="Name">The dependent's name.</param>
/// <param name="Kind">Either <c>rule</c> or <c>proposition</c>.</param>
/// <param name="Errors">Why it would no longer bind.</param>
public sealed record BrokenDependent(string Name, string Kind, IReadOnlyList<RuleError> Errors);

/// <summary>
/// A node that references a proposition and is rebound when it is republished. Distinct from
/// <see cref="BrokenDependent"/>: listing the blast radius is not reporting a failure, and reusing
/// the failure type with an empty error list would blur the two.
/// </summary>
/// <param name="Name">The dependent's name.</param>
/// <param name="Kind">Either <c>rule</c> or <c>proposition</c>.</param>
public sealed record PropositionDependent(string Name, string Kind);

/// <summary>The outcome kind of a <see cref="PropositionSet"/> mutation.</summary>
public enum PropositionUpdateOutcome
{
    /// <summary>A new proposition was authored.</summary>
    Created,

    /// <summary>An existing proposition's document was replaced.</summary>
    Updated,

    /// <summary>The authored document was withdrawn — reverting to a compiled spec, or removing it outright.</summary>
    Removed,

    /// <summary>The expected version was stale.</summary>
    VersionConflict,

    /// <summary>The document, or a dependent of it, failed to bind.</summary>
    Invalid,

    /// <summary>No proposition — authored or compiled — is known under the name.</summary>
    NotFound,

    /// <summary>A proposition is already authored under the name.</summary>
    NameTaken,

    /// <summary>Removal would leave referrers dangling.</summary>
    Referenced
}

/// <summary>
/// The result of attempting to author, replace, or withdraw a proposition. Expected outcomes are
/// values rather than exceptions, mirroring <see cref="RuleUpdateResult"/>.
/// </summary>
public sealed class PropositionUpdateResult
{
    private PropositionUpdateResult(
        PropositionUpdateOutcome outcome,
        int version,
        IReadOnlyList<RuleError> errors,
        IReadOnlyList<BrokenDependent> brokenDependents,
        IReadOnlyList<string> referrers)
    {
        Outcome = outcome;
        Version = version;
        Errors = errors;
        BrokenDependents = brokenDependents;
        Referrers = referrers;
    }

    /// <summary>The outcome kind.</summary>
    public PropositionUpdateOutcome Outcome { get; }

    /// <summary>The new version on success; the current version on <see cref="PropositionUpdateOutcome.VersionConflict"/>; otherwise 0.</summary>
    public int Version { get; }

    /// <summary>Errors in the submitted document itself; empty when the document was fine but a dependent broke.</summary>
    public IReadOnlyList<RuleError> Errors { get; }

    /// <summary>Dependents the edit would have broken; empty unless that is why it was rejected.</summary>
    public IReadOnlyList<BrokenDependent> BrokenDependents { get; }

    /// <summary>The names blocking a removal on <see cref="PropositionUpdateOutcome.Referenced"/>; otherwise empty.</summary>
    public IReadOnlyList<string> Referrers { get; }

    /// <summary>A new proposition was authored at version 1.</summary>
    public static PropositionUpdateResult Created(int version) =>
        new(PropositionUpdateOutcome.Created, version, [], [], []);

    /// <summary>The document was replaced and the proposition now has the given version.</summary>
    public static PropositionUpdateResult Updated(int version) =>
        new(PropositionUpdateOutcome.Updated, version, [], [], []);

    /// <summary>The authored document was withdrawn.</summary>
    public static PropositionUpdateResult Removed() =>
        new(PropositionUpdateOutcome.Removed, 0, [], [], []);

    /// <summary>The caller's expected version was stale.</summary>
    public static PropositionUpdateResult VersionConflict(int currentVersion) =>
        new(PropositionUpdateOutcome.VersionConflict, currentVersion, [], [], []);

    /// <summary>The submitted document failed structural or semantic binding.</summary>
    public static PropositionUpdateResult Invalid(IReadOnlyList<RuleError> errors) =>
        new(PropositionUpdateOutcome.Invalid, 0, errors, [], []);

    /// <summary>The submitted document bound, but one or more dependents would not.</summary>
    public static PropositionUpdateResult BreaksDependents(IReadOnlyList<BrokenDependent> broken) =>
        new(PropositionUpdateOutcome.Invalid, 0, [], broken, []);

    /// <summary>Nothing is known under the requested name.</summary>
    public static PropositionUpdateResult NotFound() =>
        new(PropositionUpdateOutcome.NotFound, 0, [], [], []);

    /// <summary>A proposition is already authored under the requested name.</summary>
    public static PropositionUpdateResult NameTaken() =>
        new(PropositionUpdateOutcome.NameTaken, 0, [], [], []);

    /// <summary>Removal is blocked by the given referrers.</summary>
    public static PropositionUpdateResult Referenced(IReadOnlyList<string> referrers) =>
        new(PropositionUpdateOutcome.Referenced, 0, [], [], referrers);
}
```

- [ ] **Step 7: Run tests to verify they pass**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~InMemoryPropositionStoreTests"
```

Expected: PASS, five tests.

- [ ] **Step 8: Commit**

```bash
git add src/Motiv.Serialization/Propositions/ src/Motiv.Serialization.Tests/Propositions/InMemoryPropositionStoreTests.cs
git commit -m "feat(serialization): proposition value types and persistence seam"
```

---

### Task 7: `BindingScope` — the prepare-all-then-commit-all transaction

The scope owns the four things that must agree during a publish: the layered source, the write lock, the graph, and the participants. This task builds and tests the transaction against a **stub** participant, so the mechanism is proven before `PropositionSet` or `RuleSet` depend on it.

**Files:**
- Create: `src/Motiv.Serialization/Propositions/IRebindable.cs`
- Create: `src/Motiv.Serialization/Propositions/PropositionOverlay.cs`
- Create: `src/Motiv.Serialization/Propositions/BindingScope.cs`
- Test: `src/Motiv.Serialization.Tests/Propositions/BindingScopeTests.cs` (create)

**Interfaces:**
- Consumes: `ISpecSource`, `LayeredSpecSource`, `DependencyGraph`, `NodeId`, `BrokenDependent`.
- Produces:
  ```csharp
  internal interface IRebindable
  {
      NodeId Node { get; }
      IRebindCommit? PrepareRebind(ISpecSource prospective, List<RuleError> errors);
  }
  internal interface IRebindCommit
  {
      SpecRegistryEntry? OverlayEntry { get; }   // the entry this node contributes; null for rules
      void Commit();
  }
  internal sealed class PropositionOverlay : ISpecSource
  {
      public PropositionOverlay();
      public PropositionOverlay(PropositionOverlay copyFrom);
      public void Set(SpecRegistryEntry entry);
      public void Remove(string name);
      public SpecRegistryEntry? Find(string name);
      public CollectionBinding<TParent>? FindCollection<TParent>(string path);   // always null
  }
  internal sealed class BindingScope
  {
      public BindingScope(SpecRegistry registry);
      public SpecRegistry Registry { get; }
      public DependencyGraph Graph { get; }
      public PropositionOverlay Overlay { get; }
      public ISpecSource Source { get; }
      public void Enrol(IRebindable participant);
      public void Withdraw(NodeId node);
      public T Locked<T>(Func<T> action);
      public IReadOnlyList<BrokenDependent> PrepareClosure(string propositionName, PropositionOverlay prospective, List<IRebindCommit> commits);
  }
  ```

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Serialization.Tests/Propositions/BindingScopeTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class BindingScopeTests
{
    private static SpecBase<int, string> AnySpec { get; } = Spec.Build((int n) => n > 0).Create("any");

    private static SpecRegistryEntry Entry(string name) =>
        new SpecRegistry().Register(name, AnySpec).Find(name)!;

    /// <summary>A participant that rebinds successfully, or fails on demand, and records that it committed.</summary>
    private sealed class StubParticipant(NodeId node, bool succeeds) : IRebindable, IRebindCommit
    {
        public NodeId Node { get; } = node;
        public bool Committed { get; private set; }
        public int PrepareCount { get; private set; }
        public List<string> ObservedOrder { get; } = [];

        /// <summary>Set by the test to record the global commit order.</summary>
        public List<string>? OrderLog { get; init; }

        public SpecRegistryEntry? OverlayEntry =>
            Node.Kind == NodeKind.Proposition ? Entry(Node.Name) : null;

        public IRebindCommit? PrepareRebind(ISpecSource prospective, List<RuleError> errors)
        {
            PrepareCount++;
            OrderLog?.Add(Node.Name);
            if (succeeds)
                return this;
            errors.Add(new RuleError("$", RuleErrorCode.UnknownSpec, $"{Node.Name} cannot bind"));
            return null;
        }

        public void Commit() => Committed = true;
    }

    [Fact]
    public void Should_expose_a_layered_source_over_the_registry()
    {
        // Arrange
        var registry = new SpecRegistry().Register("compiled", AnySpec);
        var scope = new BindingScope(registry);

        // Act & Assert
        scope.Source.Find("compiled").ShouldNotBeNull();
        scope.Source.Find("authored").ShouldBeNull();

        scope.Overlay.Set(Entry("authored"));
        scope.Source.Find("authored").ShouldNotBeNull();
    }

    [Fact]
    public void Should_prepare_nothing_when_the_closure_is_empty()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var commits = new List<IRebindCommit>();

        // Act
        var broken = scope.PrepareClosure("a", new PropositionOverlay(), commits);

        // Assert
        broken.ShouldBeEmpty();
        commits.ShouldBeEmpty();
    }

    [Fact]
    public void Should_prepare_every_dependent_in_dependency_order()
    {
        // Arrange — a <- b <- c
        var scope = new BindingScope(new SpecRegistry());
        var order = new List<string>();
        var b = new StubParticipant(NodeId.Proposition("b"), succeeds: true) { OrderLog = order };
        var c = new StubParticipant(NodeId.Proposition("c"), succeeds: true) { OrderLog = order };
        scope.Enrol(b);
        scope.Enrol(c);
        scope.Graph.Set(NodeId.Proposition("b"), ["a"]);
        scope.Graph.Set(NodeId.Proposition("c"), ["b"]);
        var commits = new List<IRebindCommit>();

        // Act
        var broken = scope.PrepareClosure("a", new PropositionOverlay(), commits);

        // Assert
        broken.ShouldBeEmpty();
        order.ShouldBe(["b", "c"]);
        commits.Count.ShouldBe(2);
    }

    [Fact]
    public void Should_fold_each_prepared_entry_into_the_prospective_overlay()
    {
        // Arrange — 'c' must be able to see the freshly bound 'b' while preparing
        var scope = new BindingScope(new SpecRegistry());
        var prospective = new PropositionOverlay();
        scope.Enrol(new StubParticipant(NodeId.Proposition("b"), succeeds: true));
        scope.Graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act
        scope.PrepareClosure("a", prospective, []);

        // Assert
        prospective.Find("b").ShouldNotBeNull();
    }

    [Fact]
    public void Should_report_a_broken_dependent_without_committing_anything()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var good = new StubParticipant(NodeId.Proposition("b"), succeeds: true);
        var bad = new StubParticipant(NodeId.Rule("can-checkout"), succeeds: false);
        scope.Enrol(good);
        scope.Enrol(bad);
        scope.Graph.Set(NodeId.Proposition("b"), ["a"]);
        scope.Graph.Set(NodeId.Rule("can-checkout"), ["a"]);
        var commits = new List<IRebindCommit>();

        // Act
        var broken = scope.PrepareClosure("a", new PropositionOverlay(), commits);

        // Assert
        broken.Count.ShouldBe(1);
        broken[0].Name.ShouldBe("can-checkout");
        broken[0].Kind.ShouldBe("rule");
        broken[0].Errors.ShouldNotBeEmpty();
        good.Committed.ShouldBeFalse();
        bad.Committed.ShouldBeFalse();
    }

    [Fact]
    public void Should_label_a_broken_proposition_dependent_as_a_proposition()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        scope.Enrol(new StubParticipant(NodeId.Proposition("b"), succeeds: false));
        scope.Graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act
        var broken = scope.PrepareClosure("a", new PropositionOverlay(), []);

        // Assert
        broken.Count.ShouldBe(1);
        broken[0].Kind.ShouldBe("proposition");
    }

    [Fact]
    public void Should_collect_every_broken_dependent_rather_than_stopping_at_the_first()
    {
        // Arrange — reporting only the first would make a wide break take many round trips to diagnose
        var scope = new BindingScope(new SpecRegistry());
        scope.Enrol(new StubParticipant(NodeId.Proposition("b"), succeeds: false));
        scope.Enrol(new StubParticipant(NodeId.Rule("r"), succeeds: false));
        scope.Graph.Set(NodeId.Proposition("b"), ["a"]);
        scope.Graph.Set(NodeId.Rule("r"), ["a"]);

        // Act
        var broken = scope.PrepareClosure("a", new PropositionOverlay(), []);

        // Assert
        broken.Count.ShouldBe(2);
    }

    [Fact]
    public void Should_skip_closure_members_with_no_enrolled_participant()
    {
        // Arrange — a graph edge can outlive its participant during teardown; that must not throw
        var scope = new BindingScope(new SpecRegistry());
        scope.Graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act
        var broken = scope.PrepareClosure("a", new PropositionOverlay(), []);

        // Assert
        broken.ShouldBeEmpty();
    }

    [Fact]
    public void Should_stop_preparing_a_withdrawn_participant()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var b = new StubParticipant(NodeId.Proposition("b"), succeeds: true);
        scope.Enrol(b);
        scope.Graph.Set(NodeId.Proposition("b"), ["a"]);

        // Act
        scope.Withdraw(NodeId.Proposition("b"));
        scope.PrepareClosure("a", new PropositionOverlay(), []);

        // Assert
        b.PrepareCount.ShouldBe(0);
    }

    [Fact]
    public void Should_run_the_supplied_action_under_the_lock_and_return_its_value()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());

        // Act
        var result = scope.Locked(() => 42);

        // Assert
        result.ShouldBe(42);
    }

    [Fact]
    public void Should_serialize_concurrent_locked_sections()
    {
        // Arrange — a data race here would surface as a count below the expected total
        var scope = new BindingScope(new SpecRegistry());
        var counter = 0;

        // Act
        Parallel.For(0, 200, _ => scope.Locked(() =>
        {
            var seen = counter;
            counter = seen + 1;
            return 0;
        }));

        // Assert
        counter.ShouldBe(200);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~BindingScopeTests"
```

Expected: FAIL to compile — `IRebindable`, `PropositionOverlay`, `BindingScope` do not exist.

- [ ] **Step 3: Write the rebind contracts**

Create `src/Motiv.Serialization/Propositions/IRebindable.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// A node whose binding depends on propositions, and which therefore has to be rebound when one of
/// them is republished. Implemented by authored propositions and by document-backed rules.
/// </summary>
/// <remarks>
/// Rebinding is two-phase on purpose. Preparing every member of the closure before committing any of
/// them is what makes a publish all-or-nothing: a dependent that would stop binding is discovered
/// while the live state is still untouched.
/// </remarks>
internal interface IRebindable
{
    /// <summary>This node's identity in the dependency graph.</summary>
    NodeId Node { get; }

    /// <summary>
    /// Binds against the prospective source **without publishing**. Returns null and fills
    /// <paramref name="errors"/> when the node would no longer bind.
    /// </summary>
    IRebindCommit? PrepareRebind(ISpecSource prospective, List<RuleError> errors);
}

/// <summary>A prepared rebind, ready to be published.</summary>
internal interface IRebindCommit
{
    /// <summary>
    /// The entry this node contributes to the overlay so later members of the closure resolve it,
    /// or null for a node that is not itself referenceable (a rule).
    /// </summary>
    SpecRegistryEntry? OverlayEntry { get; }

    /// <summary>Publishes the prepared binding. Must not fail.</summary>
    void Commit();
}
```

- [ ] **Step 4: Write `PropositionOverlay`**

Create `src/Motiv.Serialization/Propositions/PropositionOverlay.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// The authored layer of the layered source: names currently backed by an authored document.
/// </summary>
/// <remarks>
/// Copy-construction is how a publish stays atomic without partial mutation. A prospective overlay is
/// cloned, bound into freely, and either swapped in whole or discarded — so a failed publish cannot
/// leave half-applied entries behind. Publishes are rare, so cloning a dictionary is not a cost worth
/// optimising away.
/// </remarks>
internal sealed class PropositionOverlay : ISpecSource
{
    private readonly Dictionary<string, SpecRegistryEntry> _entries;

    public PropositionOverlay() => _entries = new Dictionary<string, SpecRegistryEntry>(StringComparer.Ordinal);

    public PropositionOverlay(PropositionOverlay copyFrom) =>
        _entries = new Dictionary<string, SpecRegistryEntry>(copyFrom._entries, StringComparer.Ordinal);

    public void Set(SpecRegistryEntry entry) => _entries[entry.Name] = entry;

    public void Remove(string name) => _entries.Remove(name);

    public SpecRegistryEntry? Find(string name) =>
        _entries.TryGetValue(name, out var entry) ? entry : null;

    // Collections are compiled-only; the layered source resolves them from the registry.
    public CollectionBinding<TParent>? FindCollection<TParent>(string path) => null;
}
```

- [ ] **Step 5: Write `BindingScope`**

Create `src/Motiv.Serialization/Propositions/BindingScope.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// The single coordinator a publish runs inside. Cascade has to be atomic across propositions *and*
/// rules — a live rule can sit in a proposition's dependent closure — so one object owns the layered
/// source, the write lock, the dependency graph, and the rebind participants.
/// </summary>
/// <remarks>
/// The lock and the version check solve different problems and both are needed. The lock is
/// machine-scale: it stops two publishes interleaving their graph walks. The version check
/// (compare-and-swap, as rules already do) is human-scale: it stops a save silently discarding an
/// edit made while a browser tab sat open.
/// </remarks>
internal sealed class BindingScope
{
    private readonly object _gate = new();
    private readonly Dictionary<NodeId, IRebindable> _participants = [];

    public BindingScope(SpecRegistry registry)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Overlay = new PropositionOverlay();
        Source = new LayeredSpecSource(Overlay, registry);
    }

    /// <summary>The immutable compiled catalog.</summary>
    public SpecRegistry Registry { get; }

    /// <summary>Who references whom.</summary>
    public DependencyGraph Graph { get; } = new();

    /// <summary>The live authored layer.</summary>
    public PropositionOverlay Overlay { get; }

    /// <summary>The live resolution order: authored first, then compiled.</summary>
    public ISpecSource Source { get; }

    /// <summary>Registers a node as rebindable. Replaces any participant already under that id.</summary>
    public void Enrol(IRebindable participant) => _participants[participant.Node] = participant;

    /// <summary>Unregisters a node, so it is no longer rebound.</summary>
    public void Withdraw(NodeId node) => _participants.Remove(node);

    /// <summary>Runs an action holding the write lock, so a publish sees a still graph.</summary>
    public T Locked<T>(Func<T> action)
    {
        lock (_gate)
            return action();
    }

    /// <summary>
    /// Prepares every node transitively affected by republishing <paramref name="propositionName"/>,
    /// in dependency order, folding each prepared entry into <paramref name="prospective"/> so later
    /// members resolve the new definitions. Commits nothing.
    /// </summary>
    /// <returns>
    /// The dependents that would stop binding — empty when the whole closure prepared, in which case
    /// <paramref name="commits"/> holds every prepared rebind in the order it should be committed.
    /// </returns>
    public IReadOnlyList<BrokenDependent> PrepareClosure(
        string propositionName, PropositionOverlay prospective, List<IRebindCommit> commits)
    {
        var prospectiveSource = new LayeredSpecSource(prospective, Registry);
        var broken = new List<BrokenDependent>();

        foreach (var node in Graph.DependentClosure(propositionName))
        {
            // A graph edge can outlive its participant while a node is being torn down.
            if (!_participants.TryGetValue(node, out var participant))
                continue;

            var errors = new List<RuleError>();
            var commit = participant.PrepareRebind(prospectiveSource, errors);

            if (commit is null)
            {
                broken.Add(new BrokenDependent(
                    node.Name,
                    node.Kind == NodeKind.Rule ? "rule" : "proposition",
                    errors));
                // Keep going: reporting only the first break would make a wide failure take many
                // round trips to diagnose.
                continue;
            }

            if (commit.OverlayEntry is { } entry)
                prospective.Set(entry);

            commits.Add(commit);
        }

        return broken;
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~BindingScopeTests"
```

Expected: PASS, eleven tests.

- [ ] **Step 7: Commit**

```bash
git add src/Motiv.Serialization/Propositions/ src/Motiv.Serialization.Tests/Propositions/BindingScopeTests.cs
git commit -m "feat(serialization): all-or-nothing rebind transaction over the dependency closure"
```

---

### Task 8: `PropositionSet` — construction, model registration, and authoring

Creating a proposition needs no cascade: a brand-new name has no referrers. It *does* need a cycle check, because a document can reference the name being created (`{"spec":"a"}` saved as `a`).

**Files:**
- Create: `src/Motiv.Serialization/Propositions/PropositionModelBinding.cs`
- Create: `src/Motiv.Serialization/Propositions/PropositionSet.cs`
- Test: `src/Motiv.Serialization.Tests/Propositions/PropositionSetCreateTests.cs` (create)

**Interfaces:**
- Consumes: `BindingScope`, `PropositionOverlay`, `IRebindable`, `DocumentReferences`, `IPropositionStore`, `PropositionUpdateResult`, `PropositionEntry`, `RuleDocumentParser`, `RuleBinder`, `AsyncRuleBinder`.
- Produces:
  ```csharp
  internal delegate SpecRegistryEntry? BindProposition(
      ISpecSource source, string name, string? description,
      RuleDocument document, bool isAsync, List<RuleError> errors);

  internal sealed class PropositionModelBinding
  { public required string Id { get; init; } public required Type ModelType { get; init; } public required BindProposition Bind { get; init; } }

  public sealed class PropositionSet
  {
      internal PropositionSet(BindingScope scope, IPropositionStore store, RuleSerializerOptions? options = null);
      public PropositionSet AddModel<TModel>(string modelTypeId);
      public IReadOnlyCollection<PropositionEntry> Propositions { get; }
      public PropositionEntry? Find(string name);
      public string? DocumentJsonOf(string name);
      public IReadOnlyList<PropositionDependent> Dependents(string name);
      public PropositionUpdateResult Create(string name, string modelTypeId, string documentJson, string? description);
  }
  ```

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Serialization.Tests/Propositions/PropositionSetCreateTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class PropositionSetCreateTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static AsyncSpecBase<Customer, string> PassesCheck { get; } =
        Spec.Build(async (Customer c) => { await Task.Yield(); return c.IsActive; })
            .WhenTrue("passes").WhenFalse("fails").Create();

    private static (PropositionSet Set, BindingScope Scope, InMemoryPropositionStore Store) NewSet()
    {
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.passes-check", PassesCheck);
        var scope = new BindingScope(registry);
        var store = new InMemoryPropositionStore();
        var set = new PropositionSet(scope, store).AddModel<Customer>("customer");
        return (set, scope, store);
    }

    [Fact]
    public void Should_create_a_proposition_at_version_1()
    {
        // Arrange
        var (set, _, _) = NewSet();

        // Act
        var result = set.Create(
            "customer.is-eligible", "customer", """{ "spec": "customer.is-active" }""", "Eligibility");

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        result.Version.ShouldBe(1);
    }

    [Fact]
    public void Should_make_a_created_proposition_resolvable_as_a_spec()
    {
        // Arrange — this is the whole point: an authored proposition becomes a building block
        var (set, scope, _) = NewSet();

        // Act
        set.Create("customer.is-eligible", "customer", """{ "spec": "customer.is-active" }""", null);

        // Assert
        var entry = scope.Source.Find("customer.is-eligible");
        entry.ShouldNotBeNull();
        entry.ModelType.ShouldBe(typeof(Customer));
        entry.MetadataType.ShouldBe(typeof(string));
        entry.IsAsync.ShouldBeFalse();
    }

    [Fact]
    public void Should_let_a_proposition_reference_another_proposition()
    {
        // Arrange
        var (set, scope, _) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);

        // Act
        var result = set.Create("customer.b", "customer", """{ "not": { "spec": "customer.a" } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        scope.Source.Find("customer.b").ShouldNotBeNull();
    }

    [Fact]
    public void Should_derive_asyncness_from_the_referenced_specs()
    {
        // Arrange
        var (set, scope, _) = NewSet();

        // Act
        set.Create("customer.screened", "customer", """{ "spec": "customer.passes-check" }""", null);

        // Assert
        scope.Source.Find("customer.screened")!.IsAsync.ShouldBeTrue();
    }

    [Fact]
    public void Should_propagate_asyncness_transitively_through_a_proposition()
    {
        // Arrange — b references a, which is async; b must be async too
        var (set, scope, _) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.passes-check" }""", null);

        // Act
        set.Create("customer.b", "customer", """{ "not": { "spec": "customer.a" } }""", null);

        // Assert
        scope.Source.Find("customer.b")!.IsAsync.ShouldBeTrue();
    }

    [Fact]
    public void Should_persist_a_created_proposition()
    {
        // Arrange
        var (set, _, store) = NewSet();

        // Act
        set.Create("customer.is-eligible", "customer", """{ "spec": "customer.is-active" }""", "why");

        // Assert
        var stored = store.Load();
        stored.Count.ShouldBe(1);
        stored[0].Name.ShouldBe("customer.is-eligible");
        stored[0].ModelType.ShouldBe("customer");
        stored[0].Version.ShouldBe(1);
        stored[0].Description.ShouldBe("why");
    }

    [Fact]
    public void Should_reject_a_name_that_violates_the_grammar()
    {
        // Arrange
        var (set, _, _) = NewSet();

        // Act
        var result = set.Create("customer..bad", "customer", """{ "spec": "customer.is-active" }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.InvalidSpecName);
    }

    [Fact]
    public void Should_reject_a_name_already_authored()
    {
        // Arrange
        var (set, _, _) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);

        // Act
        var result = set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.NameTaken);
    }

    [Fact]
    public void Should_accept_a_name_that_exists_only_as_a_compiled_spec_creating_an_override()
    {
        // Arrange — overriding a compiled spec is a create, and must not read as a name clash
        var (set, scope, _) = NewSet();

        // Act
        var result = set.Create(
            "customer.is-active", "customer", """{ "not": { "spec": "customer.passes-check" } }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        result.Version.ShouldBe(1);
        set.Find("customer.is-active")!.Origin.ShouldBe(PropositionOrigin.Overridden);
        // The overlay now shadows the compiled spec, so the effective definition is the async one.
        scope.Source.Find("customer.is-active")!.IsAsync.ShouldBeTrue();
    }

    [Fact]
    public void Should_reject_a_self_reference()
    {
        // Arrange — the only cycle a brand-new name can create
        var (set, _, _) = NewSet();

        // Act
        var result = set.Create("customer.a", "customer", """{ "spec": "customer.a" }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.CycleDetected);
    }

    [Fact]
    public void Should_reject_a_document_referencing_an_unknown_spec()
    {
        // Arrange
        var (set, _, _) = NewSet();

        // Act
        var result = set.Create("customer.a", "customer", """{ "spec": "nope" }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.UnknownSpec);
    }

    [Fact]
    public void Should_reject_an_unregistered_model_type()
    {
        // Arrange
        var (set, _, _) = NewSet();

        // Act
        var result = set.Create("order.a", "order", """{ "spec": "customer.is-active" }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.ModelTypeMismatch);
    }

    [Fact]
    public void Should_not_persist_or_publish_a_rejected_create()
    {
        // Arrange
        var (set, scope, store) = NewSet();

        // Act
        set.Create("customer.a", "customer", """{ "spec": "nope" }""", null);

        // Assert
        store.Load().ShouldBeEmpty();
        scope.Source.Find("customer.a").ShouldBeNull();
        set.Find("customer.a").ShouldBeNull();
    }

    [Fact]
    public void Should_list_compiled_and_authored_propositions_together()
    {
        // Arrange
        var (set, _, _) = NewSet();
        set.Create("customer.is-eligible", "customer", """{ "spec": "customer.is-active" }""", null);

        // Act
        var listed = set.Propositions.ToDictionary(entry => entry.Name);

        // Assert
        listed.Count.ShouldBe(3);
        listed["customer.is-active"].Origin.ShouldBe(PropositionOrigin.Compiled);
        listed["customer.is-active"].Version.ShouldBe(0);
        listed["customer.passes-check"].Origin.ShouldBe(PropositionOrigin.Compiled);
        listed["customer.is-eligible"].Origin.ShouldBe(PropositionOrigin.Authored);
        listed["customer.is-eligible"].Version.ShouldBe(1);
    }

    [Fact]
    public void Should_report_the_document_of_an_authored_proposition_and_null_for_a_compiled_one()
    {
        // Arrange
        var (set, _, _) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);

        // Act & Assert
        set.DocumentJsonOf("customer.a").ShouldNotBeNull();
        set.DocumentJsonOf("customer.is-active").ShouldBeNull();
    }

    private sealed record Customer(bool IsActive);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~PropositionSetCreateTests"
```

Expected: FAIL to compile — `PropositionSet` does not exist.

- [ ] **Step 3: Write the model binding**

Create `src/Motiv.Serialization/Propositions/PropositionModelBinding.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// Binds a proposition document for one model type. Written as a delegate so
/// <c>TModel</c> is captured at registration and binding needs no reflection — the same approach
/// the endpoints' model bindings already take.
/// </summary>
internal delegate SpecRegistryEntry? BindProposition(
    ISpecSource source,
    string name,
    string? description,
    RuleDocument document,
    bool isAsync,
    List<RuleError> errors);

/// <summary>A registered evaluable model type, with its binder closure.</summary>
internal sealed class PropositionModelBinding
{
    public required string Id { get; init; }

    public required Type ModelType { get; init; }

    public required BindProposition Bind { get; init; }
}
```

- [ ] **Step 4: Write `PropositionSet`**

Create `src/Motiv.Serialization/Propositions/PropositionSet.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// The authored propositions an application resolves alongside its compiled ones. Mirrors
/// <see cref="RuleSet"/>: validate, bind, then publish atomically, with optimistic version checks on
/// writes. Unlike a rule, a proposition is *referenceable*, so publishing one also rebinds everything
/// that references it — all of it, or none.
/// </summary>
public sealed class PropositionSet
{
    private readonly BindingScope _scope;
    private readonly IPropositionStore _store;
    private readonly RuleSerializerOptions _options;
    private readonly Dictionary<string, PropositionModelBinding> _models = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Authored> _authored = new(StringComparer.Ordinal);

    internal PropositionSet(BindingScope scope, IPropositionStore store, RuleSerializerOptions? options = null)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? new RuleSerializerOptions();
    }

    /// <summary>
    /// Registers a model type authored propositions may be written against, capturing
    /// <typeparamref name="TModel"/> behind a closure so no binding step needs reflection.
    /// </summary>
    /// <typeparam name="TModel">The CLR model type.</typeparam>
    /// <param name="modelTypeId">The stable id clients pass as <c>modelType</c>.</param>
    /// <returns>This set, to allow chained registration.</returns>
    public PropositionSet AddModel<TModel>(string modelTypeId)
    {
        _models[modelTypeId] = new PropositionModelBinding
        {
            Id = modelTypeId,
            ModelType = typeof(TModel),
            Bind = (source, name, description, document, isAsync, errors) =>
            {
                if (isAsync)
                {
                    var asyncSpec = AsyncRuleBinder.Bind<TModel>(document, source, errors);
                    return asyncSpec is null
                        ? null
                        : new SpecRegistryEntry(name, typeof(TModel), typeof(string), true, asyncSpec, description);
                }

                var spec = RuleBinder.Bind<TModel>(document, source, errors);
                return spec is null
                    ? null
                    : new SpecRegistryEntry(name, typeof(TModel), typeof(string), false, spec, description);
            }
        };
        return this;
    }

    /// <summary>
    /// Every proposition in scope — compiled, overridden and authored — as one effective listing.
    /// </summary>
    public IReadOnlyCollection<PropositionEntry> Propositions =>
        _scope.Locked(() =>
        {
            var entries = new Dictionary<string, PropositionEntry>(StringComparer.Ordinal);

            foreach (var compiled in _scope.Registry.Entries)
                entries[compiled.Name] = ToEntry(compiled, PropositionOrigin.Compiled, version: 0, []);

            foreach (var authored in _authored.Values)
                entries[authored.Name] = ToEntry(authored);

            return (IReadOnlyCollection<PropositionEntry>)[.. entries.Values];
        });

    /// <summary>One proposition's listing, or null when the name is unknown.</summary>
    public PropositionEntry? Find(string name) =>
        _scope.Locked(() =>
            _authored.TryGetValue(name, out var authored)
                ? ToEntry(authored)
                : _scope.Registry.Find(name) is { } compiled
                    ? ToEntry(compiled, PropositionOrigin.Compiled, version: 0, [])
                    : null);

    /// <summary>The authored document behind a name, or null when the name has no authored document.</summary>
    public string? DocumentJsonOf(string name) =>
        _scope.Locked(() => _authored.TryGetValue(name, out var authored) ? authored.DocumentJson : null);

    /// <summary>The nodes that reference the given proposition, transitively, in rebind order.</summary>
    public IReadOnlyList<PropositionDependent> Dependents(string name) =>
        _scope.Locked(() => (IReadOnlyList<PropositionDependent>)
            [.. _scope.Graph.DependentClosure(name)
                .Select(node => new PropositionDependent(
                    node.Name, node.Kind == NodeKind.Rule ? "rule" : "proposition"))]);

    /// <summary>
    /// Authors a new proposition. A name already carrying an authored document is a conflict; a name
    /// carrying only a compiled spec is accepted and creates an override.
    /// </summary>
    /// <param name="name">The dot-separated name.</param>
    /// <param name="modelTypeId">A registered model-type id.</param>
    /// <param name="documentJson">The rule document defining the proposition.</param>
    /// <param name="description">An optional description.</param>
    /// <returns>The outcome. Nothing is published or persisted unless it is <c>Created</c>.</returns>
    public PropositionUpdateResult Create(
        string name, string modelTypeId, string documentJson, string? description) =>
        _scope.Locked(() =>
        {
            if (_authored.ContainsKey(name))
                return PropositionUpdateResult.NameTaken();

            if (ValidateName(name) is { } nameError)
                return PropositionUpdateResult.Invalid([nameError]);

            var prepared = Prepare(name, modelTypeId, documentJson, description);
            if (prepared.Errors.Count > 0)
                return PropositionUpdateResult.Invalid(prepared.Errors);

            // A brand-new name has no referrers, so the closure is empty and there is nothing to
            // cascade to — but the document may still reference the name being created.
            Publish(new Authored(this, name, modelTypeId, documentJson, version: 1, description)
            {
                Bound = prepared.Entry,
                References = prepared.References
            });

            return PropositionUpdateResult.Created(1);
        });

    /// <summary>
    /// Validates the name against the registry's own grammar rather than a second copy of it — the
    /// two must agree exactly, because a document references the authored name the same way it
    /// references a compiled one.
    /// </summary>
    private static RuleError? ValidateName(string name) =>
        SpecRegistry.IsValidName(name)
            ? null
            : new RuleError("$.name", RuleErrorCode.InvalidSpecName,
                $"the name '{name}' is not a valid spec reference: each dot-separated segment must " +
                "start with an ASCII letter and contain only ASCII letters, digits, '-' or '_'");

    /// <summary>Parses, cycle-checks, and binds a document without publishing anything.</summary>
    private Prepared Prepare(string name, string modelTypeId, string documentJson, string? description)
    {
        var errors = new List<RuleError>();

        if (!_models.TryGetValue(modelTypeId, out var model))
        {
            errors.Add(new RuleError("$.modelType", RuleErrorCode.ModelTypeMismatch,
                $"model type '{modelTypeId}' is not registered for propositions"));
            return new Prepared(null, [], errors);
        }

        var document = new RuleDocumentParser(_options).Parse(documentJson, errors);
        if (document is null || errors.Count > 0)
            return new Prepared(null, [], errors);

        var references = DocumentReferences.From(document);

        if (_scope.Graph.FindCycle(name, references) is { } cycle)
        {
            errors.Add(new RuleError("$", RuleErrorCode.CycleDetected,
                $"publishing '{name}' would create a reference cycle: {string.Join(" → ", cycle)}"));
            return new Prepared(null, references, errors);
        }

        // Asyncness is derived: an entry's own IsAsync already accounts for anything it references,
        // so consulting the direct references is enough to know how this document must bind.
        var isAsync = references.Any(reference => _scope.Source.Find(reference) is { IsAsync: true });

        var entry = model.Bind(_scope.Source, name, description, document, isAsync, errors);
        return new Prepared(entry, references, errors);
    }

    /// <summary>Publishes an authored proposition: overlay, graph, participant, store.</summary>
    private void Publish(Authored authored)
    {
        _authored[authored.Name] = authored;
        _scope.Overlay.Set(authored.Bound!);
        _scope.Graph.Set(authored.Node, authored.References);
        _scope.Enrol(authored);
        _store.Save(new StoredProposition(
            authored.Name, authored.ModelTypeId, authored.DocumentJson, authored.Version, authored.Description));
    }

    private PropositionEntry ToEntry(Authored authored)
    {
        var origin = _scope.Registry.Find(authored.Name) is null
            ? PropositionOrigin.Authored
            : PropositionOrigin.Overridden;

        // A quarantined proposition has no binding of its own, so its shape is reported from the
        // compiled spec still resolving beneath it when there is one.
        var effective = authored.Bound ?? _scope.Registry.Find(authored.Name);

        return new PropositionEntry(
            authored.Name,
            effective?.ModelType.Name ?? authored.ModelTypeId,
            effective?.MetadataType.Name ?? nameof(String),
            effective?.IsAsync ?? false,
            origin,
            authored.Version,
            authored.Description,
            authored.Quarantine);
    }

    private static PropositionEntry ToEntry(
        SpecRegistryEntry entry, PropositionOrigin origin, int version, IReadOnlyList<RuleError> quarantine) =>
        new(entry.Name, entry.ModelType.Name, entry.MetadataType.Name, entry.IsAsync,
            origin, version, entry.Description, quarantine);

    /// <summary>The outcome of a prepare: the bound entry, its edges, and any errors.</summary>
    private readonly record struct Prepared(
        SpecRegistryEntry? Entry, IReadOnlyList<string> References, List<RuleError> Errors);

    /// <summary>
    /// One authored proposition's live state, and its participation in the rebind transaction.
    /// </summary>
    private sealed class Authored(
        PropositionSet owner, string name, string modelTypeId, string documentJson, int version, string? description)
        : IRebindable
    {
        public NodeId Node { get; } = NodeId.Proposition(name);
        public string Name { get; } = name;
        public string ModelTypeId { get; } = modelTypeId;
        public string DocumentJson { get; } = documentJson;
        public int Version { get; } = version;
        public string? Description { get; } = description;

        /// <summary>The current binding, or null while quarantined.</summary>
        public SpecRegistryEntry? Bound { get; set; }

        /// <summary>Why this proposition is excluded from the effective set, or empty.</summary>
        public IReadOnlyList<RuleError> Quarantine { get; set; } = [];

        public IReadOnlyList<string> References { get; set; } = [];

        public IRebindCommit? PrepareRebind(ISpecSource prospective, List<RuleError> errors)
        {
            if (!owner._models.TryGetValue(ModelTypeId, out var model))
            {
                errors.Add(new RuleError("$.modelType", RuleErrorCode.ModelTypeMismatch,
                    $"model type '{ModelTypeId}' is not registered for propositions"));
                return null;
            }

            var document = new RuleDocumentParser(owner._options).Parse(DocumentJson, errors);
            if (document is null || errors.Count > 0)
                return null;

            var isAsync = References.Any(reference => prospective.Find(reference) is { IsAsync: true });
            var entry = model.Bind(prospective, Name, Description, document, isAsync, errors);
            return entry is null ? null : new Commit(this, entry);
        }

        private sealed class Commit(Authored authored, SpecRegistryEntry entry) : IRebindCommit
        {
            public SpecRegistryEntry? OverlayEntry => entry;

            public void Commit()
            {
                authored.Bound = entry;
                authored.Quarantine = [];
                // The version is deliberately untouched: this proposition's document did not change,
                // so bumping it would spuriously conflict with an editor's open draft.
            }
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~PropositionSetCreateTests"
```

Expected: PASS, fifteen tests.

If `Should_reject_an_unregistered_model_type` fails with the wrong code, check that `Prepare` returns before parsing — the model lookup must come first, or a valid document for an unknown model reports parse success and then nothing.

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Serialization/Propositions/ src/Motiv.Serialization.Tests/Propositions/PropositionSetCreateTests.cs
git commit -m "feat(serialization): author propositions over a layered spec source"
```

---

### Task 9: Update, revert and delete — the cascade

**Files:**
- Modify: `src/Motiv.Serialization/Propositions/PropositionSet.cs`
- Test: `src/Motiv.Serialization.Tests/Propositions/PropositionSetUpdateTests.cs` (create)

**Interfaces:**
- Consumes: everything from Task 8.
- Produces on `PropositionSet`:
  ```csharp
  public PropositionUpdateResult Update(string name, string documentJson, int expectedVersion);
  public PropositionUpdateResult Withdraw(string name, int expectedVersion);   // revert-or-remove
  ```

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Serialization.Tests/Propositions/PropositionSetUpdateTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class PropositionSetUpdateTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static SpecBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();

    private static (PropositionSet Set, BindingScope Scope, InMemoryPropositionStore Store) NewSet()
    {
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-adult", IsAdult);
        var scope = new BindingScope(registry);
        var store = new InMemoryPropositionStore();
        var set = new PropositionSet(scope, store).AddModel<Customer>("customer");
        return (set, scope, store);
    }

    /// <summary>Evaluates whatever the layered source currently resolves for a name.</summary>
    private static bool Evaluate(BindingScope scope, string name, Customer customer)
    {
        var entry = scope.Source.Find(name).ShouldNotBeNull();
        return ((SpecBase<Customer, string>)entry.Spec).Evaluate(customer).Satisfied;
    }

    /// <summary>A participant that refuses to rebind, standing in for a rule that would break.</summary>
    private sealed class AlwaysBreaks(NodeId node) : IRebindable
    {
        public NodeId Node { get; } = node;

        public IRebindCommit? PrepareRebind(ISpecSource prospective, List<RuleError> errors)
        {
            errors.Add(new RuleError("$", RuleErrorCode.AsyncSpecInSyncLoad, "would not bind"));
            return null;
        }
    }

    [Fact]
    public void Should_update_a_document_and_bump_only_its_own_version()
    {
        // Arrange
        var (set, _, _) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);

        // Act
        var result = set.Update("customer.a", """{ "spec": "customer.is-adult" }""", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Updated);
        result.Version.ShouldBe(2);
    }

    [Fact]
    public void Should_make_a_dependent_see_the_new_definition_without_touching_it()
    {
        // Arrange — this is the feature's central claim: b is never re-saved, yet its meaning follows a
        var (set, scope, _) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);
        set.Create("customer.b", "customer", """{ "spec": "customer.a" }""", null);
        var inactiveAdult = new Customer(IsActive: false, Age: 30);
        Evaluate(scope, "customer.b", inactiveAdult).ShouldBeFalse();

        // Act — a now means "is an adult" instead of "is active"
        set.Update("customer.a", """{ "spec": "customer.is-adult" }""", 1);

        // Assert
        Evaluate(scope, "customer.b", inactiveAdult).ShouldBeTrue();
    }

    [Fact]
    public void Should_leave_a_dependents_version_alone_when_it_is_rebound()
    {
        // Arrange — bumping it would invalidate every colleague's open draft on an unrelated edit
        var (set, _, _) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);
        set.Create("customer.b", "customer", """{ "spec": "customer.a" }""", null);

        // Act
        set.Update("customer.a", """{ "spec": "customer.is-adult" }""", 1);

        // Assert
        set.Find("customer.b")!.Version.ShouldBe(1);
    }

    [Fact]
    public void Should_cascade_through_a_chain()
    {
        // Arrange — a <- b <- c, so editing a must reach c
        var (set, scope, _) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);
        set.Create("customer.b", "customer", """{ "spec": "customer.a" }""", null);
        set.Create("customer.c", "customer", """{ "spec": "customer.b" }""", null);
        var inactiveAdult = new Customer(IsActive: false, Age: 30);

        // Act
        set.Update("customer.a", """{ "spec": "customer.is-adult" }""", 1);

        // Assert
        Evaluate(scope, "customer.c", inactiveAdult).ShouldBeTrue();
    }

    [Fact]
    public void Should_reject_a_stale_expected_version()
    {
        // Arrange
        var (set, _, _) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);
        set.Update("customer.a", """{ "spec": "customer.is-adult" }""", 1);

        // Act — a second editor still holding version 1
        var result = set.Update("customer.a", """{ "spec": "customer.is-active" }""", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.VersionConflict);
        result.Version.ShouldBe(2);
    }

    [Fact]
    public void Should_report_not_found_for_a_name_with_no_authored_document()
    {
        // Arrange — a compiled spec must be overridden via Create before it can be updated
        var (set, _, _) = NewSet();

        // Act
        var result = set.Update("customer.is-active", """{ "spec": "customer.is-adult" }""", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.NotFound);
    }

    [Fact]
    public void Should_reject_an_update_that_would_close_a_cycle()
    {
        // Arrange — b references a; pointing a at b closes the loop
        var (set, _, _) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);
        set.Create("customer.b", "customer", """{ "spec": "customer.a" }""", null);

        // Act
        var result = set.Update("customer.a", """{ "spec": "customer.b" }""", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.Errors.ShouldContain(error => error.Code == RuleErrorCode.CycleDetected);
    }

    [Fact]
    public void Should_reject_the_whole_update_when_a_dependent_would_break()
    {
        // Arrange — a stubbed dependent stands in for a sync rule that cannot bind the new definition
        var (set, scope, _) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);
        scope.Locked(() =>
        {
            scope.Enrol(new AlwaysBreaks(NodeId.Rule("can-checkout")));
            scope.Graph.Set(NodeId.Rule("can-checkout"), ["customer.a"]);
            return 0;
        });

        // Act
        var result = set.Update("customer.a", """{ "spec": "customer.is-adult" }""", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.BrokenDependents.Count.ShouldBe(1);
        result.BrokenDependents[0].Name.ShouldBe("can-checkout");
        result.BrokenDependents[0].Kind.ShouldBe("rule");
    }

    [Fact]
    public void Should_leave_everything_untouched_when_a_dependent_would_break()
    {
        // Arrange
        var (set, scope, store) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);
        scope.Locked(() =>
        {
            scope.Enrol(new AlwaysBreaks(NodeId.Rule("can-checkout")));
            scope.Graph.Set(NodeId.Rule("can-checkout"), ["customer.a"]);
            return 0;
        });
        var inactiveAdult = new Customer(IsActive: false, Age: 30);

        // Act
        set.Update("customer.a", """{ "spec": "customer.is-adult" }""", 1);

        // Assert — version, binding and persisted document all unmoved
        set.Find("customer.a")!.Version.ShouldBe(1);
        Evaluate(scope, "customer.a", inactiveAdult).ShouldBeFalse();
        store.Load()[0].DocumentJson.ShouldContain("customer.is-active");
    }

    [Fact]
    public void Should_revert_an_override_to_the_compiled_spec()
    {
        // Arrange — override is-active with something that inverts it
        var (set, scope, store) = NewSet();
        set.Create("customer.is-active", "customer", """{ "not": { "spec": "customer.is-adult" } }""", null);
        var inactiveAdult = new Customer(IsActive: false, Age: 30);
        Evaluate(scope, "customer.is-active", inactiveAdult).ShouldBeFalse();

        // Act
        var result = set.Withdraw("customer.is-active", 1);

        // Assert — the compiled spec, never copied or moved, resolves again
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Removed);
        set.Find("customer.is-active")!.Origin.ShouldBe(PropositionOrigin.Compiled);
        store.Load().ShouldBeEmpty();
    }

    [Fact]
    public void Should_allow_reverting_an_override_that_others_reference()
    {
        // Arrange — referrers keep resolving, to the compiled spec beneath
        var (set, scope, _) = NewSet();
        set.Create("customer.is-active", "customer", """{ "not": { "spec": "customer.is-adult" } }""", null);
        set.Create("customer.b", "customer", """{ "spec": "customer.is-active" }""", null);

        // Act
        var result = set.Withdraw("customer.is-active", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Removed);
        Evaluate(scope, "customer.b", new Customer(IsActive: true, Age: 10)).ShouldBeTrue();
    }

    [Fact]
    public void Should_refuse_to_remove_an_authored_proposition_others_reference()
    {
        // Arrange — nothing lies beneath, so removal would leave b dangling
        var (set, _, _) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);
        set.Create("customer.b", "customer", """{ "spec": "customer.a" }""", null);

        // Act
        var result = set.Withdraw("customer.a", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Referenced);
        result.Referrers.ShouldBe(["customer.b"]);
    }

    [Fact]
    public void Should_remove_an_unreferenced_authored_proposition()
    {
        // Arrange
        var (set, scope, store) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);

        // Act
        var result = set.Withdraw("customer.a", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Removed);
        set.Find("customer.a").ShouldBeNull();
        scope.Source.Find("customer.a").ShouldBeNull();
        store.Load().ShouldBeEmpty();
    }

    [Fact]
    public void Should_reject_withdrawing_with_a_stale_version()
    {
        // Arrange
        var (set, _, _) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);
        set.Update("customer.a", """{ "spec": "customer.is-adult" }""", 1);

        // Act
        var result = set.Withdraw("customer.a", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.VersionConflict);
        result.Version.ShouldBe(2);
    }

    [Fact]
    public void Should_report_not_found_when_withdrawing_a_purely_compiled_spec()
    {
        // Arrange
        var (set, _, _) = NewSet();

        // Act
        var result = set.Withdraw("customer.is-active", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.NotFound);
    }

    [Fact]
    public void Should_stop_rebinding_a_removed_proposition()
    {
        // Arrange — a stale participant rebinding after removal would resurrect it
        var (set, scope, _) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);
        set.Create("customer.b", "customer", """{ "spec": "customer.a" }""", null);
        set.Withdraw("customer.b", 1);

        // Act
        var result = set.Update("customer.a", """{ "spec": "customer.is-adult" }""", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Updated);
        scope.Source.Find("customer.b").ShouldBeNull();
    }

    [Fact]
    public void Should_report_the_transitive_dependents_of_a_proposition()
    {
        // Arrange — a <- b <- c, for the UI's blast-radius strip
        var (set, _, _) = NewSet();
        set.Create("customer.a", "customer", """{ "spec": "customer.is-active" }""", null);
        set.Create("customer.b", "customer", """{ "spec": "customer.a" }""", null);
        set.Create("customer.c", "customer", """{ "spec": "customer.b" }""", null);

        // Act
        var dependents = set.Dependents("customer.a");

        // Assert
        dependents.Select(dependent => dependent.Name).ShouldBe(["customer.b", "customer.c"]);
        dependents.ShouldAllBe(dependent => dependent.Kind == "proposition");
    }

    private sealed record Customer(bool IsActive, int Age);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~PropositionSetUpdateTests"
```

Expected: FAIL to compile — `Update` and `Withdraw` do not exist on `PropositionSet`.

- [ ] **Step 3: Add `Update`**

In `src/Motiv.Serialization/Propositions/PropositionSet.cs`, add after `Create`:

```csharp
    /// <summary>
    /// Replaces an authored proposition's document, rebinding everything that references it. Either
    /// the whole closure rebinds and the new document is published, or nothing moves at all.
    /// </summary>
    /// <param name="name">The dot-separated name.</param>
    /// <param name="documentJson">The replacement document.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <returns>The outcome, carrying the dependents that broke when that is why it was rejected.</returns>
    public PropositionUpdateResult Update(string name, string documentJson, int expectedVersion) =>
        _scope.Locked(() =>
        {
            if (!_authored.TryGetValue(name, out var current))
                return PropositionUpdateResult.NotFound();

            if (current.Version != expectedVersion)
                return PropositionUpdateResult.VersionConflict(current.Version);

            var prepared = Prepare(name, current.ModelTypeId, documentJson, current.Description);
            if (prepared.Errors.Count > 0)
                return PropositionUpdateResult.Invalid(prepared.Errors);

            var replacement = new Authored(
                this, name, current.ModelTypeId, documentJson, current.Version + 1, current.Description)
            {
                Bound = prepared.Entry,
                References = prepared.References
            };

            // Bind the closure against a prospective overlay carrying the replacement, so a dependent
            // is checked against what it *would* resolve rather than what it resolves today.
            var prospective = new PropositionOverlay(_scope.Overlay);
            prospective.Set(prepared.Entry!);

            var commits = new List<IRebindCommit>();
            var broken = _scope.PrepareClosure(name, prospective, commits);
            if (broken.Count > 0)
                return PropositionUpdateResult.BreaksDependents(broken);

            Publish(replacement);
            foreach (var commit in commits)
            {
                commit.Commit();
                if (commit.OverlayEntry is { } entry)
                    _scope.Overlay.Set(entry);
            }

            return PropositionUpdateResult.Updated(replacement.Version);
        });
```

- [ ] **Step 4: Add `Withdraw`**

Add after `Update`:

```csharp
    /// <summary>
    /// Withdraws an authored document. When a compiled spec lies beneath the name this reverts to it
    /// — permitted even while referenced, because referrers keep resolving. When nothing lies beneath,
    /// this removes the proposition outright, which is refused while anything references it.
    /// </summary>
    /// <param name="name">The dot-separated name.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <returns>The outcome.</returns>
    public PropositionUpdateResult Withdraw(string name, int expectedVersion) =>
        _scope.Locked(() =>
        {
            if (!_authored.TryGetValue(name, out var current))
                return PropositionUpdateResult.NotFound();

            if (current.Version != expectedVersion)
                return PropositionUpdateResult.VersionConflict(current.Version);

            var compiled = _scope.Registry.Find(name);

            if (compiled is null)
            {
                // Removal would leave referrers pointing at nothing, so direct referrers block it.
                var referrers = _scope.Graph.Referrers(name);
                if (referrers.Count > 0)
                    return PropositionUpdateResult.Referenced([.. referrers.Select(node => node.Name)]);
            }
            else
            {
                // Reverting changes what referrers resolve, so it takes the same transactional check
                // as any other edit — the compiled spec may not satisfy every dependent.
                var prospective = new PropositionOverlay(_scope.Overlay);
                prospective.Remove(name);

                var commits = new List<IRebindCommit>();
                var broken = _scope.PrepareClosure(name, prospective, commits);
                if (broken.Count > 0)
                    return PropositionUpdateResult.BreaksDependents(broken);

                foreach (var commit in commits)
                {
                    commit.Commit();
                    if (commit.OverlayEntry is { } entry)
                        _scope.Overlay.Set(entry);
                }
            }

            _authored.Remove(name);
            _scope.Overlay.Remove(name);
            _scope.Graph.Remove(current.Node);
            _scope.Withdraw(current.Node);
            _store.Delete(name);

            return PropositionUpdateResult.Removed();
        });
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~PropositionSetUpdateTests"
```

Expected: PASS, seventeen tests.

If `Should_allow_reverting_an_override_that_others_reference` fails, check that the revert branch removes the name from the *prospective* overlay before preparing the closure — preparing against the live overlay would rebind `customer.b` against the override that is about to disappear.

- [ ] **Step 6: Run the whole serialization suite**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Motiv.Serialization/Propositions/PropositionSet.cs src/Motiv.Serialization.Tests/Propositions/PropositionSetUpdateTests.cs
git commit -m "feat(serialization): cascade proposition edits to every dependent, transactionally"
```

---

### Task 10: Rules join the cascade

A live rule can sit in a proposition's dependent closure, so rules must be rebindable — and their concurrency must move under the shared lock, because a rule can now be rebound by someone else's proposition edit.

**Files:**
- Modify: `src/Motiv.Serialization/Rules/RuleBase.cs`
- Modify: `src/Motiv.Serialization/Rules/Rule.cs`
- Modify: `src/Motiv.Serialization/Rules/RuleSet.cs`
- Test: `src/Motiv.Serialization.Tests/Propositions/RuleCascadeTests.cs` (create)

**Interfaces:**
- Consumes: `BindingScope`, `IRebindable`, `IRebindCommit`, `DocumentReferences`.
- Produces:
  - `internal abstract IRebindCommit? PrepareRebind(RuleSerializer serializer, List<RuleError> errors)` on `RuleBase`, sealed-overridden in `Rule<TModel, TMetadata>`.
  - `internal RuleSet(BindingScope scope, RuleSerializerOptions? options = null)`; `internal BindingScope Scope { get; }`. The public `RuleSet(SpecRegistry, RuleSerializerOptions?)` constructor is retained and creates its own scope.

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Serialization.Tests/Propositions/RuleCascadeTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class RuleCascadeTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static SpecBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();

    private static AsyncSpecBase<Customer, string> PassesCheck { get; } =
        Spec.Build(async (Customer c) => { await Task.Yield(); return c.IsActive; })
            .WhenTrue("passes").WhenFalse("fails").Create();

    private sealed class CanCheckoutRule() : Rule<Customer, string>("can-checkout", IsActive);

    private static (PropositionSet Propositions, RuleSet Rules, CanCheckoutRule Rule) NewHost()
    {
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-adult", IsAdult)
            .Register("customer.passes-check", PassesCheck);
        var scope = new BindingScope(registry);
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore()).AddModel<Customer>("customer");
        var rule = new CanCheckoutRule();
        var rules = new RuleSet(scope).Add(rule);
        return (propositions, rules, rule);
    }

    [Fact]
    public void Should_rebind_a_rule_when_a_proposition_it_references_changes()
    {
        // Arrange — the feature's central claim, now across the rule boundary
        var (propositions, rules, rule) = NewHost();
        propositions.Create("customer.eligible", "customer", """{ "spec": "customer.is-active" }""", null);
        rules.Update("can-checkout", """{ "spec": "customer.eligible" }""", 1);
        var inactiveAdult = new Customer(IsActive: false, Age: 30);
        rule.Evaluate(inactiveAdult).Satisfied.ShouldBeFalse();

        // Act — the rule is never touched again
        propositions.Update("customer.eligible", """{ "spec": "customer.is-adult" }""", 1);

        // Assert
        rule.Evaluate(inactiveAdult).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_leave_a_rebound_rules_version_alone()
    {
        // Arrange
        var (propositions, rules, rule) = NewHost();
        propositions.Create("customer.eligible", "customer", """{ "spec": "customer.is-active" }""", null);
        rules.Update("can-checkout", """{ "spec": "customer.eligible" }""", 1);
        var versionBefore = rule.Version;

        // Act
        propositions.Update("customer.eligible", """{ "spec": "customer.is-adult" }""", 1);

        // Assert — its document did not change, so neither does its version
        rule.Version.ShouldBe(versionBefore);
    }

    /// <summary>
    /// The concrete way a *valid* edit breaks a dependent, and the failure the whole transactional
    /// design exists to catch: a sync rule cannot bind a proposition that has just become async.
    /// </summary>
    [Fact]
    public void Should_reject_a_proposition_edit_that_makes_a_sync_rule_unbindable()
    {
        // Arrange
        var (propositions, rules, rule) = NewHost();
        propositions.Create("customer.eligible", "customer", """{ "spec": "customer.is-active" }""", null);
        rules.Update("can-checkout", """{ "spec": "customer.eligible" }""", 1);

        // Act — the new definition is perfectly valid on its own, but async
        var result = propositions.Update(
            "customer.eligible", """{ "spec": "customer.passes-check" }""", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Invalid);
        result.BrokenDependents.Count.ShouldBe(1);
        result.BrokenDependents[0].Name.ShouldBe("can-checkout");
        result.BrokenDependents[0].Kind.ShouldBe("rule");
        result.BrokenDependents[0].Errors
            .ShouldContain(error => error.Code == RuleErrorCode.AsyncSpecInSyncLoad);
    }

    [Fact]
    public void Should_leave_the_proposition_and_the_rule_untouched_when_the_rule_would_break()
    {
        // Arrange
        var (propositions, rules, rule) = NewHost();
        propositions.Create("customer.eligible", "customer", """{ "spec": "customer.is-active" }""", null);
        rules.Update("can-checkout", """{ "spec": "customer.eligible" }""", 1);
        var inactiveAdult = new Customer(IsActive: false, Age: 30);

        // Act
        propositions.Update("customer.eligible", """{ "spec": "customer.passes-check" }""", 1);

        // Assert
        propositions.Find("customer.eligible")!.Version.ShouldBe(1);
        rule.Evaluate(inactiveAdult).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_list_a_rule_as_a_dependent_of_the_proposition_it_references()
    {
        // Arrange
        var (propositions, rules, _) = NewHost();
        propositions.Create("customer.eligible", "customer", """{ "spec": "customer.is-active" }""", null);
        rules.Update("can-checkout", """{ "spec": "customer.eligible" }""", 1);

        // Act
        var dependents = propositions.Dependents("customer.eligible");

        // Assert
        dependents.Count.ShouldBe(1);
        dependents[0].Name.ShouldBe("can-checkout");
        dependents[0].Kind.ShouldBe("rule");
    }

    [Fact]
    public void Should_refuse_to_remove_a_proposition_a_rule_references()
    {
        // Arrange
        var (propositions, rules, _) = NewHost();
        propositions.Create("customer.eligible", "customer", """{ "spec": "customer.is-active" }""", null);
        rules.Update("can-checkout", """{ "spec": "customer.eligible" }""", 1);

        // Act
        var result = propositions.Withdraw("customer.eligible", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Referenced);
        result.Referrers.ShouldBe(["can-checkout"]);
    }

    [Fact]
    public void Should_stop_tracking_a_rule_reverted_to_its_compiled_default()
    {
        // Arrange — a compiled default references nothing, so the rule leaves the graph
        var (propositions, rules, _) = NewHost();
        propositions.Create("customer.eligible", "customer", """{ "spec": "customer.is-active" }""", null);
        rules.Update("can-checkout", """{ "spec": "customer.eligible" }""", 1);

        // Act
        rules.Revert("can-checkout", 2);

        // Assert
        propositions.Dependents("customer.eligible").ShouldBeEmpty();
        propositions.Withdraw("customer.eligible", 1).Outcome.ShouldBe(PropositionUpdateOutcome.Removed);
    }

    [Fact]
    public void Should_keep_working_when_constructed_without_a_proposition_set()
    {
        // Arrange — the public constructor must stay usable for hosts that never author propositions
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var rule = new CanCheckoutRule();

        // Act
        var rules = new RuleSet(registry).Add(rule);

        // Assert
        rules.Update("can-checkout", """{ "spec": "customer.is-active" }""", 1)
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        rule.Evaluate(new Customer(IsActive: true, Age: 30)).Satisfied.ShouldBeTrue();
    }

    private sealed record Customer(bool IsActive, int Age);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleCascadeTests"
```

Expected: FAIL to compile — `new RuleSet(scope)` has no matching constructor.

- [ ] **Step 3: Add the rebind hook to `RuleBase`**

In `src/Motiv.Serialization/Rules/RuleBase.cs`, add after `TryRevert` (line 54):

```csharp
    /// <summary>
    /// Binds the rule's current document against a prospective source without publishing, so a
    /// proposition edit can discover that this rule would stop binding while nothing has moved yet.
    /// Returns a no-op commit for a rule on its compiled default, which references nothing.
    /// </summary>
    internal abstract IRebindCommit? PrepareRebind(RuleSerializer serializer, List<RuleError> errors);
```

- [ ] **Step 4: Implement it on `Rule<TModel, TMetadata>`**

In `src/Motiv.Serialization/Rules/Rule.cs`, add after `VersionedDocument` (line 121):

```csharp
    internal sealed override IRebindCommit? PrepareRebind(RuleSerializer serializer, List<RuleError> errors)
    {
        var current = Snapshot();

        // A compiled default resolves no names, so there is nothing to rebind.
        if (current.DocumentJson is null)
            return NoRebind.Instance;

        SpecBase<TModel, TMetadata> spec;
        try
        {
            spec = Bind(serializer, current.DocumentJson);
        }
        catch (RuleSerializationException exception)
        {
            errors.AddRange(exception.Errors);
            return null;
        }

        if (RequirePolicy(spec) is { } policyError)
        {
            errors.Add(policyError);
            return null;
        }

        // The version is carried across unchanged: the document did not change, only what it resolves
        // to, so bumping it would spuriously conflict with an editor's open draft.
        return new RebindCommit(this, new State(current.DocumentJson, current.Version, spec));
    }

    /// <summary>A prepared rebind of this rule, published by swapping its state snapshot.</summary>
    private sealed class RebindCommit(Rule<TModel, TMetadata> rule, State replacement) : IRebindCommit
    {
        // A rule is not referenceable from a document, so it contributes nothing to the overlay.
        public SpecRegistryEntry? OverlayEntry => null;

        public void Commit() => Volatile.Write(ref rule._state, replacement);
    }

    /// <summary>The commit for a rule that had nothing to rebind.</summary>
    private sealed class NoRebind : IRebindCommit
    {
        public static NoRebind Instance { get; } = new();

        public SpecRegistryEntry? OverlayEntry => null;

        public void Commit()
        {
        }
    }
```

**Note:** `RebindCommit` writes `rule._state` directly rather than going through `Publish`. That is deliberate — `Publish` returns a `VersionConflict` on a compare-and-swap miss, which is meaningless here: the rebind already runs under the `BindingScope` write lock, so no competing publish can be in flight, and there is no caller version to compare against.

- [ ] **Step 5: Wire `RuleSet` to the scope**

Replace the constructor region of `src/Motiv.Serialization/Rules/RuleSet.cs` (lines 16-26) with:

```csharp
    private readonly Dictionary<string, RuleBase> _rules = new(StringComparer.Ordinal);
    private readonly RuleSerializer _serializer;
    private readonly RuleSerializerOptions? _options;

    /// <summary>Creates a rule set whose documents bind against the given registry.</summary>
    /// <param name="registry">The registry rule documents resolve spec references against.</param>
    /// <param name="options">Options forwarded to the underlying serializer, or null for defaults.</param>
    public RuleSet(SpecRegistry registry, RuleSerializerOptions? options = null)
        : this(new BindingScope(registry ?? throw new ArgumentNullException(nameof(registry))), options)
    {
    }

    /// <summary>
    /// Creates a rule set sharing a <see cref="BindingScope"/> with a <see cref="PropositionSet"/>, so
    /// a proposition edit and a rule update cannot interleave and a rule can be rebound by a
    /// proposition's republication.
    /// </summary>
    internal RuleSet(BindingScope scope, RuleSerializerOptions? options = null)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _options = options;
        _serializer = new RuleSerializer(scope.Source, options);
    }

    /// <summary>The coordinator this set publishes under.</summary>
    internal BindingScope Scope { get; }
```

- [ ] **Step 6: Enrol rules and maintain their edges**

In the same file, replace the body of `Add` (lines 42-60) so the rule joins the graph, and wrap the mutators in the lock. Replace `Add`, `Update` and `Revert` with:

```csharp
    /// <summary>
    /// Registers a rule and binds its default immediately — an invalid default document throws
    /// here, at startup, rather than at first evaluation.
    /// </summary>
    /// <param name="rule">The rule to register.</param>
    /// <returns>This rule set, to allow chained registration.</returns>
    /// <exception cref="RuleSerializationException">The rule's default document does not bind.</exception>
    public RuleSet Add(RuleBase rule)
    {
        if (rule is null) throw new ArgumentNullException(nameof(rule));
        if (_rules.ContainsKey(rule.Name))
            throw new ArgumentException($"A rule is already registered under the name '{rule.Name}'.", nameof(rule));

        return Scope.Locked(() =>
        {
            try
            {
                rule.Attach(_serializer);
            }
            catch (RuleSerializationException ex)
            {
                // Name the failing rule — a startup failure over many rules is otherwise anonymous.
                throw new RuleSerializationException($"Rule '{rule.Name}': {ex.Message}", ex.Errors);
            }

            _rules[rule.Name] = rule;
            Track(rule);
            return this;
        });
    }

    /// <summary>
    /// Replaces a rule's implementation with a document: validate → bind → atomic publish.
    /// The live rule is untouched unless the document binds and the expected version holds.
    /// </summary>
    /// <param name="name">The rule name.</param>
    /// <param name="documentJson">The replacement rule document.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <returns>The outcome: updated, version conflict, invalid document, or not found.</returns>
    public RuleUpdateResult Update(string name, string documentJson, int expectedVersion)
    {
        if (documentJson is null) throw new ArgumentNullException(nameof(documentJson));

        return Scope.Locked(() =>
        {
            if (Find(name) is not { } rule)
                return RuleUpdateResult.NotFound();

            var result = rule.TryUpdate(_serializer, documentJson, expectedVersion);
            if (result.Outcome == RuleUpdateOutcome.Updated)
                Track(rule);
            return result;
        });
    }

    /// <summary>Reverts a rule to its default. The version moves forward, never back.</summary>
    /// <param name="name">The rule name.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <returns>The outcome: updated, version conflict, invalid default document, or not found.</returns>
    public RuleUpdateResult Revert(string name, int expectedVersion) =>
        Scope.Locked(() =>
        {
            if (Find(name) is not { } rule)
                return RuleUpdateResult.NotFound();

            var result = rule.TryRevert(_serializer, expectedVersion);
            if (result.Outcome == RuleUpdateOutcome.Updated)
                Track(rule);
            return result;
        });

    /// <summary>
    /// Records the rule's current outgoing references and its participation in rebinds. A rule on a
    /// compiled default resolves no names, so it leaves the graph entirely.
    /// </summary>
    private void Track(RuleBase rule)
    {
        var node = NodeId.Rule(rule.Name);
        var references = ReferencesOf(rule.DocumentJson);

        if (references.Count == 0)
        {
            Scope.Graph.Remove(node);
            Scope.Withdraw(node);
            return;
        }

        Scope.Graph.Set(node, references);
        Scope.Enrol(new RuleParticipant(rule, _options));
    }

    private IReadOnlyList<string> ReferencesOf(string? documentJson)
    {
        if (documentJson is null)
            return [];

        var errors = new List<RuleError>();
        var document = new RuleDocumentParser(_options ?? new RuleSerializerOptions()).Parse(documentJson, errors);
        // The document has already bound by the time this runs, so a parse failure is impossible.
        return document is null ? [] : DocumentReferences.From(document);
    }

    /// <summary>Adapts a rule to the rebind transaction, supplying it a serializer over the prospective source.</summary>
    private sealed class RuleParticipant(RuleBase rule, RuleSerializerOptions? options) : IRebindable
    {
        public NodeId Node { get; } = NodeId.Rule(rule.Name);

        public IRebindCommit? PrepareRebind(ISpecSource prospective, List<RuleError> errors) =>
            rule.PrepareRebind(new RuleSerializer(prospective, options), errors);
    }
```

- [ ] **Step 7: Run tests to verify they pass**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleCascadeTests"
```

Expected: PASS, eight tests.

If `Should_reject_a_proposition_edit_that_makes_a_sync_rule_unbindable` reports no broken dependents, check that `Track` runs after a successful `Update` — an unenrolled rule is silently skipped by `PrepareClosure`, which is exactly the silent-under-reporting failure mode this test guards.

- [ ] **Step 8: Confirm the existing rule tests still pass**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0
```

Expected: PASS. The `Rule<TModel, TMetadata>` state field is now written from a nested class, so if the compiler objects that `_state` is inaccessible, confirm `RebindCommit` is nested *inside* `Rule<TModel, TMetadata>` rather than beside it.

- [ ] **Step 9: Commit**

```bash
git add src/Motiv.Serialization/Rules/ src/Motiv.Serialization.Tests/Propositions/RuleCascadeTests.cs
git commit -m "feat(serialization): rebind live rules when a proposition they reference changes"
```

---

### Task 11: Startup load and quarantine

Deliberately asymmetric with `RuleSet.Add`: a compiled default failing to bind is a developer error worth crashing on, but a persisted document failing to bind is an operational reality — a redeploy that renamed a C# spec must not turn a stale row into an outage.

**Files:**
- Modify: `src/Motiv.Serialization/Propositions/PropositionSet.cs`
- Test: `src/Motiv.Serialization.Tests/Propositions/PropositionSetLoadTests.cs` (create)

**Interfaces:**
- Consumes: everything from Tasks 8–9.
- Produces: `public void Load()` on `PropositionSet` — binds every stored document in dependency order, quarantining what fails.

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Serialization.Tests/Propositions/PropositionSetLoadTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class PropositionSetLoadTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static StoredProposition Stored(string name, string documentJson, int version = 1) =>
        new(name, "customer", documentJson, version, null);

    private static (PropositionSet Set, BindingScope Scope) Load(params StoredProposition[] stored)
    {
        var store = new InMemoryPropositionStore();
        foreach (var proposition in stored)
            store.Save(proposition);

        var scope = new BindingScope(new SpecRegistry().Register("customer.is-active", IsActive));
        var set = new PropositionSet(scope, store).AddModel<Customer>("customer");
        set.Load();
        return (set, scope);
    }

    [Fact]
    public void Should_bind_a_stored_proposition()
    {
        // Act
        var (set, scope) = Load(Stored("customer.a", """{ "spec": "customer.is-active" }"""));

        // Assert
        scope.Source.Find("customer.a").ShouldNotBeNull();
        set.Find("customer.a")!.Quarantine.ShouldBeEmpty();
    }

    [Fact]
    public void Should_preserve_the_stored_version()
    {
        // Act
        var (set, _) = Load(Stored("customer.a", """{ "spec": "customer.is-active" }""", version: 7));

        // Assert — versions must survive a restart or every reader's next save would conflict
        set.Find("customer.a")!.Version.ShouldBe(7);
    }

    [Fact]
    public void Should_bind_dependencies_before_dependents_regardless_of_store_order()
    {
        // Arrange — b depends on a, deliberately stored first
        var stored = new[]
        {
            Stored("customer.b", """{ "spec": "customer.a" }"""),
            Stored("customer.a", """{ "spec": "customer.is-active" }"""),
        };

        // Act
        var (set, scope) = Load(stored);

        // Assert
        scope.Source.Find("customer.b").ShouldNotBeNull();
        set.Find("customer.b")!.Quarantine.ShouldBeEmpty();
    }

    [Fact]
    public void Should_quarantine_a_document_referencing_a_spec_that_no_longer_exists()
    {
        // Arrange — the redeploy case: the C# spec this document referenced was renamed away
        // Act
        var (set, scope) = Load(Stored("customer.a", """{ "spec": "customer.removed-in-a-redeploy" }"""));

        // Assert
        var entry = set.Find("customer.a").ShouldNotBeNull();
        entry.Quarantine.ShouldContain(error => error.Code == RuleErrorCode.UnknownSpec);
        scope.Source.Find("customer.a").ShouldBeNull();
    }

    [Fact]
    public void Should_keep_the_document_of_a_quarantined_proposition_for_repair()
    {
        // Act
        var (set, _) = Load(Stored("customer.a", """{ "spec": "gone" }"""));

        // Assert
        set.DocumentJsonOf("customer.a").ShouldNotBeNull();
    }

    [Fact]
    public void Should_quarantine_a_dependent_of_a_quarantined_proposition()
    {
        // Arrange
        var stored = new[]
        {
            Stored("customer.a", """{ "spec": "gone" }"""),
            Stored("customer.b", """{ "spec": "customer.a" }"""),
        };

        // Act
        var (set, scope) = Load(stored);

        // Assert
        set.Find("customer.b")!.Quarantine.ShouldNotBeEmpty();
        scope.Source.Find("customer.b").ShouldBeNull();
    }

    [Fact]
    public void Should_let_a_compiled_spec_resolve_beneath_a_quarantined_override()
    {
        // Arrange — a broken override must reveal the compiled spec, not a hole
        // Act
        var (set, scope) = Load(Stored("customer.is-active", """{ "spec": "gone" }"""));

        // Assert
        set.Find("customer.is-active")!.Quarantine.ShouldNotBeEmpty();
        var entry = scope.Source.Find("customer.is-active").ShouldNotBeNull();
        entry.Spec.ShouldBeSameAs(IsActive);
    }

    [Fact]
    public void Should_load_the_healthy_propositions_alongside_the_quarantined_ones()
    {
        // Arrange — one bad row must not cost the whole store
        var stored = new[]
        {
            Stored("customer.broken", """{ "spec": "gone" }"""),
            Stored("customer.fine", """{ "spec": "customer.is-active" }"""),
        };

        // Act
        var (set, scope) = Load(stored);

        // Assert
        scope.Source.Find("customer.fine").ShouldNotBeNull();
        scope.Source.Find("customer.broken").ShouldBeNull();
    }

    [Fact]
    public void Should_never_throw_on_a_malformed_stored_document()
    {
        // Arrange — a hand-edited JSON file must not stop the application booting
        var store = new InMemoryPropositionStore();
        store.Save(Stored("customer.a", "{ not json"));
        var scope = new BindingScope(new SpecRegistry().Register("customer.is-active", IsActive));
        var set = new PropositionSet(scope, store).AddModel<Customer>("customer");

        // Act
        var load = () => set.Load();

        // Assert
        load.ShouldNotThrow();
        set.Find("customer.a")!.Quarantine.ShouldNotBeEmpty();
    }

    [Fact]
    public void Should_allow_repairing_a_quarantined_proposition_by_updating_it()
    {
        // Arrange
        var (set, scope) = Load(Stored("customer.a", """{ "spec": "gone" }""", version: 3));

        // Act
        var result = set.Update("customer.a", """{ "spec": "customer.is-active" }""", 3);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Updated);
        set.Find("customer.a")!.Quarantine.ShouldBeEmpty();
        scope.Source.Find("customer.a").ShouldNotBeNull();
    }

    [Fact]
    public void Should_allow_deleting_a_quarantined_proposition()
    {
        // Arrange
        var (set, _) = Load(Stored("customer.a", """{ "spec": "gone" }""", version: 2));

        // Act
        var result = set.Withdraw("customer.a", 2);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Removed);
        set.Find("customer.a").ShouldBeNull();
    }

    private sealed record Customer(bool IsActive);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~PropositionSetLoadTests"
```

Expected: FAIL to compile — `Load` does not exist.

- [ ] **Step 3: Add `Load`**

In `src/Motiv.Serialization/Propositions/PropositionSet.cs`, add after `Withdraw`:

```csharp
    /// <summary>
    /// Reads every persisted proposition and binds it, in dependency order. A document that fails to
    /// bind — or that depends on one which did — is *quarantined* rather than fatal: it is excluded
    /// from the effective set with its errors recorded, its document retained for repair, and any
    /// compiled spec beneath the name left to resolve in its place.
    /// </summary>
    /// <remarks>
    /// This is deliberately asymmetric with <see cref="RuleSet.Add"/>, which fails fast. A compiled
    /// default failing to bind is a developer error and should stop startup. A persisted document
    /// failing to bind is an operational reality — a redeploy renames a C# spec a saved proposition
    /// referenced — and refusing to boot would turn a stale row into an outage. Call once, before
    /// rules are added, so a rule's default document may reference an authored proposition.
    /// </remarks>
    public void Load() =>
        _scope.Locked(() =>
        {
            var stored = new Dictionary<string, StoredProposition>(StringComparer.Ordinal);
            var references = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var parseErrors = new Dictionary<string, List<RuleError>>(StringComparer.Ordinal);

            foreach (var proposition in _store.Load())
            {
                stored[proposition.Name] = proposition;

                // Parsed up front purely to order the binding; parse failures are carried forward so
                // the document is still listed, quarantined, rather than silently dropped.
                var errors = new List<RuleError>();
                var document = SafeParse(proposition.DocumentJson, errors);
                references[proposition.Name] = document is null ? [] : DocumentReferences.From(document);
                if (document is null || errors.Count > 0)
                    parseErrors[proposition.Name] = errors;
            }

            foreach (var name in OrderByDependency(stored.Keys, references))
                LoadOne(stored[name], references[name], parseErrors.GetValueOrDefault(name));

            return 0;
        });

    /// <summary>Binds one stored proposition, publishing it or quarantining it.</summary>
    private void LoadOne(StoredProposition stored, IReadOnlyList<string> references, List<RuleError>? parseErrors)
    {
        var authored = new Authored(
            this, stored.Name, stored.ModelType, stored.DocumentJson, stored.Version, stored.Description)
        {
            References = references
        };

        _authored[stored.Name] = authored;

        if (parseErrors is { Count: > 0 })
        {
            authored.Quarantine = parseErrors;
            return;
        }

        var errors = new List<RuleError>();
        var commit = authored.PrepareRebind(_scope.Source, errors);

        if (commit is null)
        {
            // Quarantined: no overlay entry and no graph edges, so nothing resolves *to* it and
            // nothing is rebound *because* of it. Any compiled spec under the name still resolves.
            authored.Quarantine = errors;
            return;
        }

        commit.Commit();
        _scope.Overlay.Set(authored.Bound!);
        _scope.Graph.Set(authored.Node, references);
        _scope.Enrol(authored);
    }

    /// <summary>Parses without letting malformed JSON escape — a hand-edited store must not stop startup.</summary>
    private RuleDocument? SafeParse(string documentJson, List<RuleError> errors)
    {
        try
        {
            return new RuleDocumentParser(_options).Parse(documentJson, errors);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            errors.Add(new RuleError("$", RuleErrorCode.InvalidNode,
                $"the stored document could not be read: {exception.Message}"));
            return null;
        }
    }

    /// <summary>
    /// Orders stored names so a proposition follows every stored proposition it references. Names
    /// outside the store (compiled specs, or references that no longer resolve) are simply not edges.
    /// </summary>
    private static IReadOnlyList<string> OrderByDependency(
        IEnumerable<string> names, IReadOnlyDictionary<string, IReadOnlyList<string>> references)
    {
        var ordered = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in names)
            Visit(name);

        return ordered;

        void Visit(string name)
        {
            // `visiting` guards a cycle in the *stored* data, which the live graph would have
            // rejected but a hand-edited store can still contain.
            if (!visited.Add(name) || !visiting.Add(name))
                return;

            foreach (var reference in references.GetValueOrDefault(name, []))
            {
                if (references.ContainsKey(reference))
                    Visit(reference);
            }

            visiting.Remove(name);
            ordered.Add(name);
        }
    }
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~PropositionSetLoadTests"
```

Expected: PASS, eleven tests.

If `Should_never_throw_on_a_malformed_stored_document` still throws, the parser is raising `JsonException` from inside `Parse` rather than recording an error — that is exactly what `SafeParse` exists to absorb, so check the `catch` is wrapping the `Parse` call and not just its result.

- [ ] **Step 5: Run the full serialization suite**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Serialization/Propositions/PropositionSet.cs src/Motiv.Serialization.Tests/Propositions/PropositionSetLoadTests.cs
git commit -m "feat(serialization): quarantine unbindable stored propositions instead of crashing"
```

---

## Phase 3 — HTTP surface and host wiring

### Task 12: DI wiring and the effective catalog

Wiring comes before the endpoints so there is something to serve them from, and it settles the one ordering constraint in the whole system: **propositions must load before rule defaults bind**, or a rule default may not reference an authored proposition.

**Files:**
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesOptions.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs:41-74`
- Modify: `src/Motiv.Serialization.AspNetCore/RulesContracts.cs`
- Test: `src/Motiv.Serialization.AspNetCore.Tests/PropositionCatalogTests.cs` (create)

**Interfaces:**
- Consumes: `PropositionSet`, `BindingScope`, `IPropositionStore`, `PropositionOrigin`.
- Produces:
  - `CatalogEntry` gains a trailing `PropositionOrigin Origin` parameter.
  - `MotivRulesBuilder.AddPropositions(IPropositionStore? store = null)`.
  - `PropositionSet` and `BindingScope` resolvable from DI.
  - `internal IEnumerable<Action<PropositionSet>> PropositionModelRegistrations` on `MotivRulesOptions`.

- [ ] **Step 1: Write the failing test**

Create `src/Motiv.Serialization.AspNetCore.Tests/PropositionCatalogTests.cs`. Match the existing conventions in that project — check an existing test file there for how the host is built, and follow it rather than the sketch below if they differ.

```csharp
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Motiv.Serialization.AspNetCore;

namespace Motiv.Serialization.AspNetCore.Tests;

public class PropositionCatalogTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    [Fact]
    public void Should_resolve_a_proposition_set_sharing_the_rule_sets_scope()
    {
        // Arrange
        var services = new ServiceCollection();
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        services.AddMotivRules(registry, new MotivRulesOptions().AddModel<Customer>("customer"))
            .AddPropositions();
        var provider = services.BuildServiceProvider();

        // Act
        var propositions = provider.GetRequiredService<PropositionSet>();
        var rules = provider.GetRequiredService<RuleSet>();

        // Assert — a shared scope is what makes the cascade atomic across both
        propositions.Create("customer.derived", "customer", """{ "spec": "customer.is-active" }""", null)
            .Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        rules.ShouldNotBeNull();
    }

    [Fact]
    public void Should_register_every_model_with_the_proposition_set()
    {
        // Arrange — options.AddModel<T> must reach PropositionSet.AddModel<T> without reflection
        var services = new ServiceCollection();
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        services.AddMotivRules(registry, new MotivRulesOptions().AddModel<Customer>("customer"))
            .AddPropositions();
        var provider = services.BuildServiceProvider();

        // Act
        var result = provider.GetRequiredService<PropositionSet>()
            .Create("customer.derived", "customer", """{ "spec": "customer.is-active" }""", null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Created);
    }

    [Fact]
    public void Should_load_stored_propositions_before_rule_defaults_bind()
    {
        // Arrange — a rule whose *default* document references an authored proposition only binds
        // if propositions loaded first, so this pins the startup ordering.
        var store = new InMemoryPropositionStore();
        store.Save(new StoredProposition(
            "customer.derived", "customer", """{ "spec": "customer.is-active" }""", 1, null));

        var services = new ServiceCollection();
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        services.AddMotivRules(registry, new MotivRulesOptions().AddModel<Customer>("customer"))
            .AddPropositions(store)
            .AddRule(new DerivedRule());
        var provider = services.BuildServiceProvider();

        // Act
        var resolve = () => provider.GetRequiredService<RuleSet>();

        // Assert
        resolve.ShouldNotThrow();
    }

    [Fact]
    public async Task Should_include_authored_propositions_in_the_catalog()
    {
        // Arrange — the regression guard for the catalog being a closed-over constant
        var builder = WebApplication.CreateBuilder();
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        builder.Services
            .AddMotivRules(registry, new MotivRulesOptions().AddModel<Customer>("customer"))
            .AddPropositions();
        var app = builder.Build();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();

        try
        {
            app.Services.GetRequiredService<PropositionSet>()
                .Create("customer.derived", "customer", """{ "spec": "customer.is-active" }""", null);

            // Act
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
            var catalog = await client.GetFromJsonAsync<CatalogPeek>("/api/rules/catalog");

            // Assert
            catalog.ShouldNotBeNull();
            catalog.Specs.Select(spec => spec.Name).ShouldContain("customer.derived");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Should_tag_catalog_entries_with_their_origin()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        builder.Services
            .AddMotivRules(registry, new MotivRulesOptions().AddModel<Customer>("customer"))
            .AddPropositions();
        var app = builder.Build();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();

        try
        {
            app.Services.GetRequiredService<PropositionSet>()
                .Create("customer.derived", "customer", """{ "spec": "customer.is-active" }""", null);

            // Act
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
            var catalog = await client.GetFromJsonAsync<CatalogPeek>("/api/rules/catalog");

            // Assert
            var byName = catalog!.Specs.ToDictionary(spec => spec.Name);
            byName["customer.is-active"].Origin.ShouldBe("Compiled");
            byName["customer.derived"].Origin.ShouldBe("Authored");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private sealed record CatalogPeek(IReadOnlyList<SpecPeek> Specs);

    private sealed record SpecPeek(string Name, string Origin);

    private sealed class DerivedRule() : Rule<Customer, string>(
        "derived-rule", RuleDocuments.Json("""{ "spec": "customer.derived" }"""));

    private sealed record Customer(bool IsActive);
}
```

**Before running:** confirm `RuleDocuments` exposes a factory taking raw JSON. Read `src/Motiv.Serialization/Rules/RuleDocuments.cs` and use whichever member it actually provides (`Embedded(string)` is referenced in `Rule`'s XML docs at `Rule.cs:33`); adjust `DerivedRule` to match rather than inventing a member.

- [ ] **Step 2: Run test to verify it fails**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.AspNetCore.Tests -f net10.0 --filter "FullyQualifiedName~PropositionCatalogTests"
```

Expected: FAIL to compile — `AddPropositions` does not exist.

- [ ] **Step 3: Record proposition model registrations on the options**

In `src/Motiv.Serialization.AspNetCore/MotivRulesOptions.cs`, add a field beside the others (line 10):

```csharp
    private readonly List<Action<PropositionSet>> _propositionModels = [];
```

Inside `AddModel<TModel>`, immediately before `return this;` (line 70):

```csharp
        // Recorded as a closure so PropositionSet.AddModel<TModel> is reached with TModel intact —
        // the alternative would be reflecting over the Type, which this codebase avoids on principle.
        _propositionModels.Add(propositions => propositions.AddModel<TModel>(id));
```

And add beside `ModelBindings` (line 77):

```csharp
    /// <summary>Replays each AddModel call onto a PropositionSet, preserving the generic argument.</summary>
    internal IEnumerable<Action<PropositionSet>> PropositionModelRegistrations => _propositionModels;
```

- [ ] **Step 4: Add `AddPropositions` and share the scope**

In `src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs`, replace the `AddMotivRules` body's service registrations (lines 68-82) with:

```csharp
        services.AddSingleton(registry);
        services.AddSingleton(options);
        services.AddSingleton(provider => new BindingScope(provider.GetRequiredService<SpecRegistry>()));
        services.AddSingleton(provider =>
        {
            // Resolve from the provider rather than closing over the parameters, so the
            // RuleSet always shares whatever registry/options the endpoints resolve —
            // even if a later registration shadowed the ones passed here.
            var resolvedOptions = provider.GetRequiredService<MotivRulesOptions>();

            // Propositions load first: a rule's *default* document may reference an authored
            // proposition, and Add binds that default immediately.
            provider.GetService<PropositionSet>();

            var rules = new RuleSet(
                provider.GetRequiredService<BindingScope>(),
                resolvedOptions.SerializerOptions);
            foreach (var rule in provider.GetServices<RuleBase>())
                rules.Add(rule);
            return rules;
        });
        return new MotivRulesBuilder(services);
```

Then add to `MotivRulesBuilder`:

```csharp
    /// <summary>
    /// Enables runtime-authored propositions, backed by the given store (in-memory when omitted).
    /// The <see cref="PropositionSet"/> shares the <see cref="RuleSet"/>'s coordinator, so a
    /// proposition edit and a rule update can never interleave.
    /// </summary>
    /// <param name="store">Where authored propositions persist, or null for in-memory.</param>
    /// <returns>This builder, to allow chained registration.</returns>
    public MotivRulesBuilder AddPropositions(IPropositionStore? store = null)
    {
        Services.AddSingleton(store ?? new InMemoryPropositionStore());
        Services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<MotivRulesOptions>();
            var propositions = new PropositionSet(
                provider.GetRequiredService<BindingScope>(),
                provider.GetRequiredService<IPropositionStore>(),
                options.SerializerOptions);

            foreach (var register in options.PropositionModelRegistrations)
                register(propositions);

            propositions.Load();
            return propositions;
        });
        return this;
    }
```

**Note:** `PropositionSet`'s constructor is `internal`, and `BindingScope` is an internal type — both are in `Motiv.Serialization`, so `Motiv.Serialization.AspNetCore` needs `InternalsVisibleTo`. Check whether it is already granted:

```bash
grep -rn "InternalsVisibleTo" src/Motiv.Serialization/ Directory.Build.props
```

If `Motiv.Serialization.AspNetCore` is not listed, add it alongside the existing entries. If the project prefers not to widen internal visibility, make `BindingScope` and `PropositionSet`'s constructor `public` instead and note the wider surface — but prefer `InternalsVisibleTo`, since `BindingScope` is an implementation detail no consumer should construct.

- [ ] **Step 5: Make the catalog effective and origin-tagged**

In `src/Motiv.Serialization.AspNetCore/RulesContracts.cs`, add a parameter to `CatalogEntry` (line 11):

```csharp
/// <param name="Origin">Whether the spec is compiled, overridden by an authored document, or authored.</param>
public sealed record CatalogEntry(
    string Name, string ModelType, string MetadataType, bool IsAsync, string? Description, PropositionOrigin Origin);
```

In `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs`, delete the `specs` local (lines 41-48) and the `catalog` local's use of it, then replace the `MapGet("/catalog", …)` line (line 74) with a handler that projects on each request:

```csharp
        // Built per request rather than closed over: authoring a proposition changes the effective
        // spec list, and a constant catalog would hide every new proposition until restart.
        var propositions = endpoints.ServiceProvider.GetService<PropositionSet>();

        group.MapGet("/catalog", () => Results.Json(
            new CatalogResponse(EffectiveSpecs(), collections, metadataTypes, modelTypes), json));

        IReadOnlyList<CatalogEntry> EffectiveSpecs()
        {
            if (propositions is null)
            {
                return [.. registry.Entries.Select(entry => new CatalogEntry(
                    entry.Name,
                    options.ResolveModelId(entry.ModelType),
                    entry.MetadataType.Name,
                    entry.IsAsync,
                    entry.Description,
                    PropositionOrigin.Compiled))];
            }

            // PropositionSet.Propositions is already the layered view: compiled, overridden and
            // authored folded into one effective listing, with quarantined entries reported as the
            // compiled spec still resolving beneath them.
            return [.. propositions.Propositions
                .Where(entry => entry.Quarantine.Count == 0 || entry.Origin == PropositionOrigin.Overridden)
                .Select(entry => new CatalogEntry(
                    entry.Name, entry.ModelType, entry.MetadataType, entry.IsAsync,
                    entry.Description, entry.Origin))];
        }
```

Keep `var catalog = …` removed — if a `catalog` local remains unused the build will warn.

`PropositionEntry.ModelType` is already a resolved id-or-type-name string coming from `PropositionSet`, so no further `ResolveModelId` call is needed on that path.

- [ ] **Step 6: Run the test to verify it passes**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.AspNetCore.Tests -f net10.0 --filter "FullyQualifiedName~PropositionCatalogTests"
```

Expected: PASS, five tests.

- [ ] **Step 7: Run both backend suites**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.Tests -f net10.0 && dotnet test src/Motiv.Serialization.AspNetCore.Tests -f net10.0
```

Expected: PASS. Existing catalog tests asserting on `CatalogEntry`'s shape will need the new `Origin` argument — update them to pass `PropositionOrigin.Compiled`.

- [ ] **Step 8: Commit**

```bash
git add src/Motiv.Serialization.AspNetCore/ src/Motiv.Serialization.AspNetCore.Tests/PropositionCatalogTests.cs src/Motiv.Serialization/
git commit -m "feat(aspnetcore): serve the effective spec catalog and wire the proposition set"
```

---

### Task 13: The proposition endpoints

**Files:**
- Create: `src/Motiv.Serialization.AspNetCore/PropositionsContracts.cs`
- Create: `src/Motiv.Serialization.AspNetCore/MotivPropositionEndpoints.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs` (call into the new mapper)
- Test: `src/Motiv.Serialization.AspNetCore.Tests/PropositionEndpointTests.cs` (create)

**Interfaces:**
- Consumes: `PropositionSet`, `PropositionUpdateResult`, `PropositionEntry`.
- Produces:
  ```csharp
  public sealed record PropositionListEntry(string Name, string ModelType, string MetadataType, bool IsAsync, string Origin, int Version, string? Description, IReadOnlyList<RuleError> Quarantine);
  public sealed record PropositionGetResponse(JsonElement? Document, int Version, string Origin, bool HasCompiledDefault);
  public sealed record PropositionCreateRequest(string Name, string ModelType, JsonElement Document, string? Description);
  public sealed record PropositionPutRequest(JsonElement Document, int BaseVersion);
  public sealed record PropositionSaveResponse(int Version);
  public sealed record CascadeFailureResponse(IReadOnlyList<RuleError> Errors, IReadOnlyList<BrokenDependent> BrokenDependents);
  public sealed record PropositionReferencedResponse(IReadOnlyList<string> Referrers);
  public sealed record DependentsResponse(IReadOnlyList<DependentEntry> Dependents);
  public sealed record DependentEntry(string Name, string Kind);
  internal static class MotivPropositionEndpoints { internal static void MapPropositionEndpoints(RouteGroupBuilder group, PropositionSet propositions, JsonSerializerOptions json); }
  ```

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Serialization.AspNetCore.Tests/PropositionEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore.Tests;

public class PropositionEndpointTests : IAsyncLifetime
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static SpecBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-adult", IsAdult);
        builder.Services
            .AddMotivRules(registry, new MotivRulesOptions().AddModel<Customer>("customer"))
            .AddPropositions();
        _app = builder.Build();
        _app.MapMotivRules("/api/rules");
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
    }

    private Task<HttpResponseMessage> Create(string name, string document, string modelType = "customer") =>
        _client.PostAsJsonAsync("/api/rules/propositions", new
        {
            name, modelType, document = JsonDocument.Parse(document).RootElement, description = (string?)null,
        });

    private Task<HttpResponseMessage> Put(string name, string document, int baseVersion) =>
        _client.PutAsJsonAsync($"/api/rules/propositions/{name}", new
        {
            document = JsonDocument.Parse(document).RootElement, baseVersion,
        });

    [Fact]
    public async Task Should_create_a_proposition_with_201()
    {
        // Act
        var response = await Create("customer.derived", """{ "spec": "customer.is-active" }""");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<PropositionSaveResponse>();
        body!.Version.ShouldBe(1);
    }

    [Fact]
    public async Task Should_reject_a_duplicate_name_with_409()
    {
        // Arrange
        await Create("customer.derived", """{ "spec": "customer.is-active" }""");

        // Act
        var response = await Create("customer.derived", """{ "spec": "customer.is-adult" }""");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Should_accept_creating_an_override_of_a_compiled_spec()
    {
        // Act — "taken" means an authored document exists, not that the name is known at all
        var response = await Create("customer.is-active", """{ "spec": "customer.is-adult" }""");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Should_reject_an_invalid_document_with_400_and_typed_errors()
    {
        // Act
        var response = await Create("customer.derived", """{ "spec": "nope" }""");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<CascadeFailureResponse>();
        body!.Errors.ShouldContain(error => error.Code == RuleErrorCode.UnknownSpec);
    }

    [Fact]
    public async Task Should_list_compiled_and_authored_propositions_with_their_origin()
    {
        // Arrange
        await Create("customer.derived", """{ "spec": "customer.is-active" }""");

        // Act
        var listed = await _client.GetFromJsonAsync<List<PropositionListEntry>>("/api/rules/propositions");

        // Assert
        var byName = listed!.ToDictionary(entry => entry.Name);
        byName["customer.is-active"].Origin.ShouldBe("Compiled");
        byName["customer.derived"].Origin.ShouldBe("Authored");
        byName["customer.derived"].Version.ShouldBe(1);
    }

    [Fact]
    public async Task Should_get_an_authored_propositions_document()
    {
        // Arrange
        await Create("customer.derived", """{ "spec": "customer.is-active" }""");

        // Act
        var body = await _client.GetFromJsonAsync<PropositionGetResponse>("/api/rules/propositions/customer.derived");

        // Assert
        body!.Version.ShouldBe(1);
        body.Origin.ShouldBe("Authored");
        body.HasCompiledDefault.ShouldBeFalse();
        body.Document.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_report_a_compiled_proposition_as_having_no_document()
    {
        // Act
        var body = await _client.GetFromJsonAsync<PropositionGetResponse>("/api/rules/propositions/customer.is-active");

        // Assert
        body!.Document.ShouldBeNull();
        body.Origin.ShouldBe("Compiled");
        body.HasCompiledDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_return_404_for_an_unknown_name()
    {
        // Act
        var response = await _client.GetAsync("/api/rules/propositions/absent");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_update_a_proposition_and_return_the_new_version()
    {
        // Arrange
        await Create("customer.derived", """{ "spec": "customer.is-active" }""");

        // Act
        var response = await Put("customer.derived", """{ "spec": "customer.is-adult" }""", 1);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<PropositionSaveResponse>())!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_reject_a_stale_base_version_with_409()
    {
        // Arrange
        await Create("customer.derived", """{ "spec": "customer.is-active" }""");
        await Put("customer.derived", """{ "spec": "customer.is-adult" }""", 1);

        // Act
        var response = await Put("customer.derived", """{ "spec": "customer.is-active" }""", 1);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<RuleConflictResponse>();
        body!.CurrentVersion.ShouldBe(2);
    }

    [Fact]
    public async Task Should_reject_a_non_positive_base_version_with_400()
    {
        // Arrange
        await Create("customer.derived", """{ "spec": "customer.is-active" }""");

        // Act
        var response = await Put("customer.derived", """{ "spec": "customer.is-adult" }""", 0);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_report_broken_dependents_when_an_edit_would_break_one()
    {
        // Arrange — b depends on a, then a is pointed at a name that does not exist
        await Create("customer.a", """{ "spec": "customer.is-active" }""");
        await Create("customer.b", """{ "spec": "customer.a" }""");

        // Act — a valid-looking edit that breaks a's own binding surfaces as document errors;
        // to break a *dependent*, remove what it relies on via a cycle-free async switch instead.
        var response = await Put("customer.a", """{ "spec": "nope" }""", 1);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<CascadeFailureResponse>();
        body!.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Should_report_the_transitive_dependents_of_a_proposition()
    {
        // Arrange
        await Create("customer.a", """{ "spec": "customer.is-active" }""");
        await Create("customer.b", """{ "spec": "customer.a" }""");
        await Create("customer.c", """{ "spec": "customer.b" }""");

        // Act
        var body = await _client.GetFromJsonAsync<DependentsResponse>(
            "/api/rules/propositions/customer.a/dependents");

        // Assert
        body!.Dependents.Select(dependent => dependent.Name).ShouldBe(["customer.b", "customer.c"]);
        body.Dependents.ShouldAllBe(dependent => dependent.Kind == "proposition");
    }

    [Fact]
    public async Task Should_delete_an_unreferenced_proposition()
    {
        // Arrange
        await Create("customer.derived", """{ "spec": "customer.is-active" }""");

        // Act
        var response = await _client.DeleteAsync("/api/rules/propositions/customer.derived?baseVersion=1");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _client.GetAsync("/api/rules/propositions/customer.derived")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_refuse_to_delete_a_referenced_proposition_with_409_listing_referrers()
    {
        // Arrange
        await Create("customer.a", """{ "spec": "customer.is-active" }""");
        await Create("customer.b", """{ "spec": "customer.a" }""");

        // Act
        var response = await _client.DeleteAsync("/api/rules/propositions/customer.a?baseVersion=1");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<PropositionReferencedResponse>();
        body!.Referrers.ShouldBe(["customer.b"]);
    }

    [Fact]
    public async Task Should_revert_an_override_to_its_compiled_spec()
    {
        // Arrange
        await Create("customer.is-active", """{ "spec": "customer.is-adult" }""");

        // Act
        var response = await _client.DeleteAsync("/api/rules/propositions/customer.is-active?baseVersion=1");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await _client.GetFromJsonAsync<PropositionGetResponse>(
            "/api/rules/propositions/customer.is-active");
        body!.Origin.ShouldBe("Compiled");
        body.Document.ShouldBeNull();
    }

    private sealed record Customer(bool IsActive, int Age);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.AspNetCore.Tests -f net10.0 --filter "FullyQualifiedName~PropositionEndpointTests"
```

Expected: FAIL to compile — the contract records do not exist.

- [ ] **Step 3: Write the contracts**

Create `src/Motiv.Serialization.AspNetCore/PropositionsContracts.cs`:

```csharp
using System.Text.Json;

namespace Motiv.Serialization.AspNetCore;

/// <summary>A listing of one proposition in scope, compiled or authored.</summary>
/// <param name="Name">The dot-separated name.</param>
/// <param name="ModelType">The registered model-type id, or the CLR type name when not registered.</param>
/// <param name="MetadataType">The metadata type name (e.g. String).</param>
/// <param name="IsAsync">Whether the effective definition evaluates asynchronously.</param>
/// <param name="Origin">Compiled, Overridden, or Authored.</param>
/// <param name="Version">The authored document's version, or 0 for a purely compiled proposition.</param>
/// <param name="Description">An optional human-readable description.</param>
/// <param name="Quarantine">
/// Binding errors that excluded an authored document from the effective set; empty when it bound.
/// Orthogonal to <paramref name="Origin"/> — an overridden or authored proposition can be quarantined.
/// </param>
public sealed record PropositionListEntry(
    string Name, string ModelType, string MetadataType, bool IsAsync,
    string Origin, int Version, string? Description, IReadOnlyList<RuleError> Quarantine);

/// <summary>One proposition's authored document and version.</summary>
/// <param name="Document">The authored document, or null when the name is served by a compiled spec.</param>
/// <param name="Version">The version; pass it back as <c>baseVersion</c> when updating. 0 when compiled.</param>
/// <param name="Origin">Compiled, Overridden, or Authored.</param>
/// <param name="HasCompiledDefault">Whether deleting would revert to a compiled spec rather than remove.</param>
public sealed record PropositionGetResponse(
    JsonElement? Document, int Version, string Origin, bool HasCompiledDefault);

/// <summary>A request to author a new proposition.</summary>
/// <param name="Name">The dot-separated name. A name already carrying an authored document conflicts.</param>
/// <param name="ModelType">A model-type id registered on the server.</param>
/// <param name="Document">The rule document defining the proposition.</param>
/// <param name="Description">An optional description.</param>
public sealed record PropositionCreateRequest(
    string Name, string ModelType, JsonElement Document, string? Description);

/// <summary>A request to replace an authored proposition's document.</summary>
/// <param name="Document">The replacement document.</param>
/// <param name="BaseVersion">The version the caller last observed; a stale value yields 409.</param>
public sealed record PropositionPutRequest(JsonElement Document, int BaseVersion);

/// <summary>A successful create, update, or withdrawal.</summary>
/// <param name="Version">The new version, or 0 after a withdrawal.</param>
public sealed record PropositionSaveResponse(int Version);

/// <summary>
/// A rejected write. <paramref name="Errors"/> holds faults in the submitted document itself;
/// <paramref name="BrokenDependents"/> holds the dependents the edit would have stopped binding.
/// The two are separate because a <see cref="RuleError"/>'s path points into *this* document and
/// cannot address a break somewhere else.
/// </summary>
public sealed record CascadeFailureResponse(
    IReadOnlyList<RuleError> Errors, IReadOnlyList<BrokenDependent> BrokenDependents);

/// <summary>A refused removal: the proposition is still referenced.</summary>
/// <param name="Referrers">The names that must stop referencing it first.</param>
public sealed record PropositionReferencedResponse(IReadOnlyList<string> Referrers);

/// <summary>One node that would be rebound by editing a proposition.</summary>
/// <param name="Name">The dependent's name.</param>
/// <param name="Kind">Either <c>rule</c> or <c>proposition</c>.</param>
public sealed record DependentEntry(string Name, string Kind);

/// <summary>The transitive blast radius of editing a proposition, in rebind order.</summary>
/// <param name="Dependents">Every affected rule and proposition.</param>
public sealed record DependentsResponse(IReadOnlyList<DependentEntry> Dependents);
```

- [ ] **Step 4: Write the endpoints**

Create `src/Motiv.Serialization.AspNetCore/MotivPropositionEndpoints.cs`:

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// The six proposition endpoints. Kept beside rather than inside
/// <see cref="MotivRulesEndpoints"/>, which is already long enough that adding a second CRUD surface
/// to it would bury both.
/// </summary>
internal static class MotivPropositionEndpoints
{
    internal static void MapPropositionEndpoints(
        RouteGroupBuilder group, PropositionSet propositions, JsonSerializerOptions json)
    {
        group.MapGet("/propositions", () =>
            Results.Json(propositions.Propositions
                .Select(entry => new PropositionListEntry(
                    entry.Name, entry.ModelType, entry.MetadataType, entry.IsAsync,
                    entry.Origin.ToString(), entry.Version, entry.Description, entry.Quarantine))
                .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                .ToArray(), json));

        group.MapGet("/propositions/{name}", (string name) =>
        {
            if (propositions.Find(name) is not { } entry)
                return Unknown(name, json);

            JsonElement? document = null;
            if (propositions.DocumentJsonOf(name) is { } documentJson)
            {
                using var parsed = JsonDocument.Parse(documentJson);
                document = parsed.RootElement.Clone();
            }

            return Results.Json(new PropositionGetResponse(
                document,
                entry.Version,
                entry.Origin.ToString(),
                entry.Origin != PropositionOrigin.Authored), json);
        });

        group.MapPost("/propositions", (PropositionCreateRequest request) =>
        {
            if (request.Document.ValueKind == JsonValueKind.Undefined)
                return MissingDocument(json);
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.Json(new ErrorResponse("The request must include a name."), json, statusCode: 400);

            var result = propositions.Create(
                request.Name, request.ModelType, request.Document.GetRawText(), request.Description);

            return result.Outcome == PropositionUpdateOutcome.Created
                ? Results.Json(new PropositionSaveResponse(result.Version), json, statusCode: 201)
                : ToFailure(result, request.Name, json);
        });

        group.MapPut("/propositions/{name}", (string name, PropositionPutRequest request) =>
        {
            if (request.Document.ValueKind == JsonValueKind.Undefined)
                return MissingDocument(json);
            if (request.BaseVersion <= 0)
                return NonPositiveBaseVersion(json);

            var result = propositions.Update(name, request.Document.GetRawText(), request.BaseVersion);

            return result.Outcome == PropositionUpdateOutcome.Updated
                ? Results.Json(new PropositionSaveResponse(result.Version), json)
                : ToFailure(result, name, json);
        });

        group.MapDelete("/propositions/{name}", (string name, int baseVersion) =>
        {
            if (baseVersion <= 0)
                return NonPositiveBaseVersion(json);

            var result = propositions.Withdraw(name, baseVersion);

            return result.Outcome == PropositionUpdateOutcome.Removed
                ? Results.Json(new PropositionSaveResponse(0), json)
                : ToFailure(result, name, json);
        });

        group.MapGet("/propositions/{name}/dependents", (string name) =>
            propositions.Find(name) is null
                ? Unknown(name, json)
                : Results.Json(new DependentsResponse(
                    [.. propositions.Dependents(name).Select(d => new DependentEntry(d.Name, d.Kind))]), json));
    }

    private static IResult ToFailure(PropositionUpdateResult result, string name, JsonSerializerOptions json) =>
        result.Outcome switch
        {
            PropositionUpdateOutcome.VersionConflict =>
                Results.Json(new RuleConflictResponse(result.Version), json, statusCode: 409),
            PropositionUpdateOutcome.NameTaken =>
                Results.Json(new ErrorResponse($"A proposition is already authored under '{name}'."), json, statusCode: 409),
            PropositionUpdateOutcome.Referenced =>
                Results.Json(new PropositionReferencedResponse(result.Referrers), json, statusCode: 409),
            PropositionUpdateOutcome.NotFound => Unknown(name, json),
            _ => Results.Json(
                new CascadeFailureResponse(result.Errors, result.BrokenDependents), json, statusCode: 400)
        };

    private static IResult Unknown(string name, JsonSerializerOptions json) =>
        Results.Json(new ErrorResponse($"Unknown proposition '{name}'."), json, statusCode: 404);

    private static IResult MissingDocument(JsonSerializerOptions json) =>
        Results.Json(new ErrorResponse("The request must include a document."), json, statusCode: 400);

    private static IResult NonPositiveBaseVersion(JsonSerializerOptions json) =>
        Results.Json(
            new ErrorResponse("baseVersion must be a positive integer; versions start at 1."),
            json, statusCode: 400);
}
```

- [ ] **Step 5: Mount them**

In `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs`, immediately after the `if (rules is not null) MapRuleEndpoints(…);` line (line 116-117), add:

```csharp
        if (propositions is not null)
            MotivPropositionEndpoints.MapPropositionEndpoints(group, propositions, json);
```

`propositions` is the local already introduced in Task 12 Step 5.

**Note on route matching:** `/propositions/{name}` and `/propositions/{name}/dependents` are distinct templates, so ASP.NET Core routes them unambiguously. Dotted names need no encoding in a path segment, but the client must still `encodeURIComponent` them (Task 16) so a name is never split by a stray character.

- [ ] **Step 6: Run tests to verify they pass**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/Motiv.Serialization.AspNetCore.Tests -f net10.0 --filter "FullyQualifiedName~PropositionEndpointTests"
```

Expected: PASS, seventeen tests.

- [ ] **Step 7: Commit**

```bash
git add src/Motiv.Serialization.AspNetCore/ src/Motiv.Serialization.AspNetCore.Tests/PropositionEndpointTests.cs
git commit -m "feat(aspnetcore): CRUD and blast-radius endpoints for authored propositions"
```

---

### Task 14: A file-backed store in the sample host

**Files:**
- Create: `src/examples/Motiv.RulesEngine.Sample/JsonFilePropositionStore.cs`
- Modify: `src/examples/Motiv.RulesEngine.Sample/Program.cs`
- Test: `src/examples/Motiv.RulesEngine.Sample.Tests/JsonFilePropositionStoreTests.cs` (create)

**Interfaces:**
- Consumes: `IPropositionStore`, `StoredProposition`.
- Produces: `public sealed class JsonFilePropositionStore(string path) : IPropositionStore`.

- [ ] **Step 1: Write the failing tests**

Create `src/examples/Motiv.RulesEngine.Sample.Tests/JsonFilePropositionStoreTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.RulesEngine.Sample.Tests;

public class JsonFilePropositionStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"motiv-propositions-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static StoredProposition Stored(string name, int version = 1) =>
        new(name, "customer", """{ "spec": "is-active" }""", version, "a description");

    [Fact]
    public void Should_report_no_propositions_when_the_file_is_absent()
    {
        // Act & Assert — a first run must not need the file to exist
        new JsonFilePropositionStore(_path).Load().ShouldBeEmpty();
    }

    [Fact]
    public void Should_persist_across_instances()
    {
        // Arrange
        new JsonFilePropositionStore(_path).Save(Stored("customer.a"));

        // Act — a second instance stands in for a restart
        var loaded = new JsonFilePropositionStore(_path).Load();

        // Assert
        loaded.Count.ShouldBe(1);
        loaded[0].Name.ShouldBe("customer.a");
        loaded[0].Description.ShouldBe("a description");
    }

    [Fact]
    public void Should_replace_a_proposition_of_the_same_name()
    {
        // Arrange
        var store = new JsonFilePropositionStore(_path);
        store.Save(Stored("customer.a", version: 1));

        // Act
        store.Save(Stored("customer.a", version: 2));

        // Assert
        var loaded = new JsonFilePropositionStore(_path).Load();
        loaded.Count.ShouldBe(1);
        loaded[0].Version.ShouldBe(2);
    }

    [Fact]
    public void Should_delete_a_proposition()
    {
        // Arrange
        var store = new JsonFilePropositionStore(_path);
        store.Save(Stored("customer.a"));
        store.Save(Stored("customer.b"));

        // Act
        store.Delete("customer.a");

        // Assert
        new JsonFilePropositionStore(_path).Load()
            .Select(proposition => proposition.Name).ShouldBe(["customer.b"]);
    }

    [Fact]
    public void Should_treat_an_unreadable_file_as_empty_rather_than_throwing()
    {
        // Arrange — a hand-edited file must not stop the sample booting
        File.WriteAllText(_path, "{ not json");

        // Act
        var load = () => new JsonFilePropositionStore(_path).Load();

        // Assert
        load.ShouldNotThrow();
        load().ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/examples/Motiv.RulesEngine.Sample.Tests -f net10.0 --filter "FullyQualifiedName~JsonFilePropositionStoreTests"
```

Expected: FAIL to compile — `JsonFilePropositionStore` does not exist.

- [ ] **Step 3: Write the store**

Create `src/examples/Motiv.RulesEngine.Sample/JsonFilePropositionStore.cs`:

```csharp
using System.Text.Json;
using Motiv.Serialization;

/// <summary>
/// Seam: proposition persistence. The library keeps authored propositions in memory and delegates
/// durability to a store, exactly as it delegates transport — swap this for a database and nothing
/// else changes.
/// </summary>
/// <remarks>
/// Rewrites the whole file on every save. Authoring is a human-paced operation, so the simplicity is
/// worth more here than incremental writes would be. Calls arrive while the publish lock is held, so
/// this must stay quick.
/// </remarks>
public sealed class JsonFilePropositionStore(string path) : IPropositionStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly object _gate = new();

    /// <inheritdoc />
    public IReadOnlyList<StoredProposition> Load()
    {
        lock (_gate)
            return ReadAll();
    }

    /// <inheritdoc />
    public void Save(StoredProposition proposition)
    {
        lock (_gate)
        {
            var propositions = ReadAll().Where(existing => existing.Name != proposition.Name).ToList();
            propositions.Add(proposition);
            Write(propositions);
        }
    }

    /// <inheritdoc />
    public void Delete(string name)
    {
        lock (_gate)
            Write([.. ReadAll().Where(existing => existing.Name != name)]);
    }

    private List<StoredProposition> ReadAll()
    {
        if (!File.Exists(path))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<StoredProposition>>(File.ReadAllText(path), Json) ?? [];
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            // A hand-edited or half-written file must not stop the app booting. The library
            // quarantines documents that fail to bind; an unreadable file is the same problem one
            // layer down, and the same answer applies.
            return [];
        }
    }

    private void Write(List<StoredProposition> propositions) =>
        File.WriteAllText(path, JsonSerializer.Serialize(propositions, Json));
}
```

- [ ] **Step 4: Wire it into the sample**

In `src/examples/Motiv.RulesEngine.Sample/Program.cs`, replace the `AddMotivRules` chain (lines 71-74) with:

```csharp
// Seam: live rules. Each AddRule enrolls a sealed rule class as a DI singleton and in the
// RuleSet behind GET/PUT/DELETE /api/rules/rules — the app executes the same instances the
// UI hot-swaps, with optimistic-concurrency protection on writes.
//
// Seam: authored propositions. AddPropositions enables the propositions endpoints and points them
// at a store. Propositions load before rule defaults bind, so a rule's default document may
// reference one. The path is configurable so a container can mount it on a volume.
var propositionsPath = builder.Configuration["Propositions:Path"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "propositions.json");

builder.Services.AddMotivRules(registry, options)
    .AddPropositions(new JsonFilePropositionStore(propositionsPath))
    .AddRule<CanCheckoutRule>()
    .AddRule<FraudScreeningRule>()
    .AddRule<LoyaltyDiscountRule>();
```

- [ ] **Step 5: Give the sample namespaced spec names**

Still in `Program.cs`, rename the registered specs so the demo's tree has something to show, and update the descriptions' surrounding comment. Change the five `Register` names (lines 9, 17, 23, 30, 39) to:

| Old | New |
|---|---|
| `is-active` | `customer.is-active` |
| `is-adult` | `customer.is-adult` |
| `has-orders` | `customer.has-orders` |
| `is-large-order` | `order.is-large` |
| `passes-credit-check` | `customer.passes-credit-check` |

- [ ] **Step 6: Update everything referencing the old names**

```bash
grep -rn "is-active\|is-adult\|has-orders\|is-large-order\|passes-credit-check" \
  src/examples/Motiv.RulesEngine.Sample src/examples/Motiv.RulesEngine.Sample.Tests ui/apps/demo/src ui/apps/demo/tests 2>/dev/null
```

Update every hit to the new name: `AppRules.cs` rule defaults, the sample tests, the demo's `App.tsx` initial store document, and any Playwright fixtures. Add `propositions.json` to `.gitignore` — it is host state, not source.

- [ ] **Step 7: Run the sample tests**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test src/examples/Motiv.RulesEngine.Sample.Tests -f net10.0
```

Expected: PASS.

- [ ] **Step 8: Run the whole solution**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test Motiv.slnx -f net10.0
```

Expected: PASS. Per CLAUDE.md the example projects assert on justification strings, so a renamed spec can shift assertion text — fix any that surface.

- [ ] **Step 9: Commit**

```bash
git add src/examples/ .gitignore ui/apps/demo/src
git commit -m "feat(sample): persist authored propositions to a JSON file and namespace the catalog"
```

---

## Phase 4 — `@motiv/rules-core`

All three tasks run from `ui/`. The workspace uses pnpm; run `pnpm install` once if `node_modules` is stale.

### Task 15: Dotted names in the DSL

**Files:**
- Modify: `ui/packages/rules-core/src/dsl/lexer.ts:7-9`
- Test: `ui/packages/rules-core/test/dsl-lexer.test.ts`, `ui/packages/rules-core/test/dsl-roundtrip.test.ts`

**Interfaces:**
- Consumes: nothing.
- Produces: `tokenize` emitting a single `spec` token for a dotted name.

- [ ] **Step 1: Write the failing tests**

Append to `ui/packages/rules-core/test/dsl-lexer.test.ts` (inside its existing top-level `describe`, matching the file's style):

```typescript
  it('lexes a dotted spec name as one token', () => {
    const tokens = tokenize('customer.eligibility.is-active');

    expect(tokens).toHaveLength(1);
    expect(tokens[0]!.kind).toBe('spec');
    expect(tokens[0]!.value).toBe('customer.eligibility.is-active');
  });

  it('lexes dotted names either side of an operator', () => {
    const tokens = tokenize('customer.is-active & customer.is-adult');

    expect(tokens.map((token) => token.value)).toEqual([
      'customer.is-active',
      '&',
      'customer.is-adult',
    ]);
  });

  it('still lexes a decimal number rather than a dotted word', () => {
    const tokens = tokenize('2.5');

    expect(tokens).toHaveLength(1);
    expect(tokens[0]!.kind).toBe('number');
    expect(tokens[0]!.value).toBe('2.5');
  });

  it('does not let a dotted word swallow a following number', () => {
    const tokens = tokenize('customer.is-active 2');

    expect(tokens.map((token) => [token.kind, token.value])).toEqual([
      ['spec', 'customer.is-active'],
      ['number', '2'],
    ]);
  });

  it('keeps a quantifier a quantifier and a dotted word a spec', () => {
    const tokens = tokenize('all all.things');

    expect(tokens.map((token) => token.kind)).toEqual(['quantifier', 'spec']);
  });
```

Append to `ui/packages/rules-core/test/dsl-roundtrip.test.ts`, following that file's existing round-trip helper (read it first and reuse its helper rather than writing a new one):

```typescript
  it('round-trips a composition of dotted names', () => {
    const text = 'customer.is-active & !order.is-large';

    // Use the file's existing parse → print helper here.
    expect(roundTrip(text)).toBe(text);
  });
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd ui && pnpm --filter @motiv/rules-core test
```

Expected: FAIL — the dotted-name tests report three tokens (`customer`, an `error` for `.`, `is-active`) instead of one.

- [ ] **Step 3: Admit the dot**

In `ui/packages/rules-core/src/dsl/lexer.ts`, replace lines 7-9 with:

```typescript
/**
 * Words are spec-shaped: a letter followed by letters, digits, hyphens or underscores — plus dots,
 * which namespace a spec name (`customer.eligibility.is-active`). A dot cannot be stolen from a
 * numeric literal, because numbers are lexed before words.
 */
const WORD_START = /[A-Za-z_]/;
const WORD_REST = /[A-Za-z0-9_.-]/;
```

Note the `.` sits before the trailing `-` inside the character class, where it is a literal rather than a range endpoint.

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd ui && pnpm --filter @motiv/rules-core test
```

Expected: PASS, including every pre-existing lexer, parser and printer test.

- [ ] **Step 5: Typecheck**

```bash
cd ui && pnpm --filter @motiv/rules-core exec tsc --noEmit
```

Expected: no output.

- [ ] **Step 6: Commit**

```bash
git add ui/packages/rules-core/src/dsl/lexer.ts ui/packages/rules-core/test/
git commit -m "feat(rules-core): lex dotted spec names as single words"
```

---

### Task 16: Proposition contracts and client methods

**Files:**
- Modify: `ui/packages/rules-core/src/contracts.ts`
- Modify: `ui/packages/rules-core/src/client.ts`
- Modify: `ui/packages/rules-core/src/index.ts` (export the new types if it re-exports explicitly)
- Test: `ui/packages/rules-core/test/client.test.ts`

**Interfaces:**
- Consumes: the endpoints from Task 13.
- Produces on `RulesApiClient`:
  ```typescript
  listPropositions(): Promise<PropositionListEntry[]>
  getProposition(name: string): Promise<PropositionGetResponse>
  createProposition(request: PropositionCreateRequest): Promise<PropositionSaveResult>
  putProposition(name: string, document: RuleDocument, baseVersion: number): Promise<PropositionSaveResult>
  deleteProposition(name: string, baseVersion: number): Promise<PropositionSaveResult>
  getDependents(name: string): Promise<DependentEntry[]>
  ```

- [ ] **Step 1: Write the failing tests**

Append to `ui/packages/rules-core/test/client.test.ts` inside the existing `describe('RulesApiClient', …)`:

```typescript
  it('lists propositions', async () => {
    const entries = [{
      name: 'customer.is-active', modelType: 'customer', metadataType: 'String',
      isAsync: false, origin: 'Compiled', version: 0, description: null, quarantine: [],
    }];
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(entries));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.listPropositions();

    expect(result).toEqual(entries);
    expect(fetchMock).toHaveBeenCalledWith('/api/rules/propositions', { method: 'GET' });
  });

  it('encodes a dotted name in the path', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({
      document: null, version: 0, origin: 'Compiled', hasCompiledDefault: true,
    }));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    await client.getProposition('customer.eligibility.is-active');

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/rules/propositions/customer.eligibility.is-active', { method: 'GET' });
  });

  it('creates a proposition and reports the new version', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ version: 1 }, 201));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.createProposition({
      name: 'customer.derived', modelType: 'customer',
      document: { spec: 'customer.is-active' }, description: null,
    });

    expect(result).toEqual({ outcome: 'saved', version: 1 });
  });

  it('reports a duplicate name as a typed outcome rather than throwing', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse({ error: "A proposition is already authored under 'customer.derived'." }, 409));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.createProposition({
      name: 'customer.derived', modelType: 'customer',
      document: { spec: 'customer.is-active' }, description: null,
    });

    expect(result.outcome).toBe('nameTaken');
  });

  it('reports a stale base version as a conflict', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ currentVersion: 3 }, 409));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.putProposition('customer.a', { spec: 'customer.is-active' }, 1);

    expect(result).toEqual({ outcome: 'conflict', currentVersion: 3 });
  });

  it('reports broken dependents separately from document errors', async () => {
    const body = {
      errors: [],
      brokenDependents: [{ name: 'can-checkout', kind: 'rule', errors: [
        { path: '$', code: 'AsyncSpecInSyncLoad', message: 'would not bind' }] }],
    };
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(body, 400));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.putProposition('customer.a', { spec: 'customer.is-active' }, 1);

    expect(result.outcome).toBe('invalid');
    if (result.outcome !== 'invalid') throw new Error('unreachable');
    expect(result.brokenDependents[0]!.name).toBe('can-checkout');
    expect(result.errors).toEqual([]);
  });

  it('reports referrers blocking a delete', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ referrers: ['customer.b'] }, 409));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.deleteProposition('customer.a', 1);

    expect(result.outcome).toBe('referenced');
    if (result.outcome !== 'referenced') throw new Error('unreachable');
    expect(result.referrers).toEqual(['customer.b']);
  });

  it('gets the transitive dependents of a proposition', async () => {
    const dependents = [{ name: 'customer.b', kind: 'proposition' }];
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ dependents }));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    const result = await client.getDependents('customer.a');

    expect(result).toEqual(dependents);
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/rules/propositions/customer.a/dependents', { method: 'GET' });
  });
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd ui && pnpm --filter @motiv/rules-core test
```

Expected: FAIL — `client.listPropositions is not a function`.

- [ ] **Step 3: Add the contract types**

Append to `ui/packages/rules-core/src/contracts.ts`:

```typescript
/** Where a proposition's current definition comes from. */
export type PropositionOrigin = 'Compiled' | 'Overridden' | 'Authored';

/** One proposition in scope, compiled or authored. */
export interface PropositionListEntry {
  name: string;
  modelType: string;
  metadataType: string;
  isAsync: boolean;
  origin: PropositionOrigin;
  version: number;
  description: string | null;
  /**
   * Binding errors that excluded an authored document from the effective set; empty when it bound.
   * Orthogonal to `origin` — an overridden or an authored proposition can each be quarantined.
   */
  quarantine: RuleError[];
}

/** One proposition's authored document and version. */
export interface PropositionGetResponse {
  /** Null when the name is served by a compiled spec. */
  document: RuleDocument | null;
  /** 0 when the proposition is purely compiled. */
  version: number;
  origin: PropositionOrigin;
  /** Whether deleting reverts to a compiled spec rather than removing outright. */
  hasCompiledDefault: boolean;
}

/** A request to author a new proposition. */
export interface PropositionCreateRequest {
  name: string;
  modelType: string;
  document: RuleDocument;
  description: string | null;
}

/** One node that would be rebound by editing a proposition. */
export interface DependentEntry {
  name: string;
  kind: 'rule' | 'proposition';
}

/**
 * The outcome of a proposition write. Every expected failure is a value rather than a throw, so the
 * UI can render it — `errors` are faults in the submitted document, `brokenDependents` are the
 * dependents the edit would have stopped binding, and the two are distinct because an error's path
 * points into *this* document and cannot address a break elsewhere.
 */
export type PropositionSaveResult =
  | { outcome: 'saved'; version: number }
  | { outcome: 'conflict'; currentVersion: number }
  | { outcome: 'invalid'; errors: RuleError[]; brokenDependents: BrokenDependent[] }
  | { outcome: 'nameTaken' }
  | { outcome: 'referenced'; referrers: string[] };

/** A dependent an attempted edit would have stopped binding. */
export interface BrokenDependent {
  name: string;
  kind: 'rule' | 'proposition';
  errors: RuleError[];
}
```

Also add `origin` to the existing `CatalogEntry` interface (`contracts.ts:4`) so the builder's spec picker can badge operands:

```typescript
  /** Whether the spec is compiled, overridden by an authored document, or authored. */
  origin: PropositionOrigin;
```

`RuleDocument` is declared in `document.ts`; add it to `contracts.ts`'s imports if it is not already imported there.

- [ ] **Step 4: Add the client methods**

In `ui/packages/rules-core/src/client.ts`, extend the type-only import with the new names, then add after `revertRule`:

```typescript
  /** GET {baseUrl}/propositions */
  async listPropositions(): Promise<PropositionListEntry[]> {
    const response = await this.#fetch(`${this.#baseUrl}/propositions`, { method: 'GET' });
    return this.#read<PropositionListEntry[]>(response);
  }

  /** GET {baseUrl}/propositions/{name} */
  async getProposition(name: string): Promise<PropositionGetResponse> {
    const response = await this.#fetch(
      `${this.#baseUrl}/propositions/${encodeURIComponent(name)}`, { method: 'GET' });
    return this.#read<PropositionGetResponse>(response);
  }

  /** POST {baseUrl}/propositions — 400/409 return typed outcomes rather than throwing. */
  async createProposition(request: PropositionCreateRequest): Promise<PropositionSaveResult> {
    return this.#readPropositionResult(await this.#post('/propositions', request));
  }

  /** PUT {baseUrl}/propositions/{name} — 400/409 return typed outcomes rather than throwing. */
  async putProposition(
    name: string, document: RuleDocument, baseVersion: number,
  ): Promise<PropositionSaveResult> {
    const response = await this.#fetch(
      `${this.#baseUrl}/propositions/${encodeURIComponent(name)}`,
      {
        method: 'PUT',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ document, baseVersion }),
      },
    );
    return this.#readPropositionResult(response);
  }

  /**
   * DELETE {baseUrl}/propositions/{name}?baseVersion=N — reverts to the compiled spec when one
   * exists, otherwise removes the proposition (refused while anything references it).
   */
  async deleteProposition(name: string, baseVersion: number): Promise<PropositionSaveResult> {
    const response = await this.#fetch(
      `${this.#baseUrl}/propositions/${encodeURIComponent(name)}?baseVersion=${baseVersion}`,
      { method: 'DELETE' },
    );
    return this.#readPropositionResult(response);
  }

  /** GET {baseUrl}/propositions/{name}/dependents */
  async getDependents(name: string): Promise<DependentEntry[]> {
    const response = await this.#fetch(
      `${this.#baseUrl}/propositions/${encodeURIComponent(name)}/dependents`, { method: 'GET' });
    return (await this.#read<{ dependents: DependentEntry[] }>(response)).dependents;
  }

  async #readPropositionResult(response: Response): Promise<PropositionSaveResult> {
    if (response.ok) {
      const body = (await response.json()) as { version: number };
      return { outcome: 'saved', version: body.version };
    }

    const body = (await response.json().catch(() => undefined)) as
      | { currentVersion?: number; referrers?: string[]; errors?: RuleError[];
          brokenDependents?: BrokenDependent[]; error?: string }
      | undefined;

    if (response.status === 409) {
      // Three different 409s share the status but not the shape, so they are told apart by body.
      if (body && typeof body.currentVersion === 'number') {
        return { outcome: 'conflict', currentVersion: body.currentVersion };
      }
      if (body?.referrers) return { outcome: 'referenced', referrers: body.referrers };
      return { outcome: 'nameTaken' };
    }

    if (response.status === 400 && body && ('errors' in body || 'brokenDependents' in body)) {
      return {
        outcome: 'invalid',
        errors: body.errors ?? [],
        brokenDependents: body.brokenDependents ?? [],
      };
    }

    const message = body?.error ?? `Request failed (${response.status}).`;
    throw new RulesApiError(response.status, message);
  }
```

**Note:** `encodeURIComponent` leaves `.` untouched, which is why the dotted-name test expects an unescaped path — the call is there to protect against any other character a name might one day carry, not to escape dots.

- [ ] **Step 5: Export the new types**

Check whether `ui/packages/rules-core/src/index.ts` re-exports contract types by name. If it does, add `PropositionOrigin`, `PropositionListEntry`, `PropositionGetResponse`, `PropositionCreateRequest`, `PropositionSaveResult`, `BrokenDependent` and `DependentEntry` alongside the existing ones.

- [ ] **Step 6: Run tests and typecheck**

```bash
cd ui && pnpm --filter @motiv/rules-core test && pnpm --filter @motiv/rules-core exec tsc --noEmit
```

Expected: PASS, and no typecheck output. Existing catalog tests constructing a `CatalogEntry` now need `origin` — add `origin: 'Compiled'`.

- [ ] **Step 7: Commit**

```bash
git add ui/packages/rules-core/src ui/packages/rules-core/test/client.test.ts
git commit -m "feat(rules-core): client and contracts for the proposition endpoints"
```

---

### Task 17: `buildNamespaceTree` and `filterTree`

Pure functions, so the explorer's logic is testable without React. The tree is a projection of the dotted name and nothing else.

**Files:**
- Create: `ui/packages/rules-core/src/namespaceTree.ts`
- Modify: `ui/packages/rules-core/src/index.ts`
- Test: `ui/packages/rules-core/test/namespaceTree.test.ts` (create)

**Interfaces:**
- Consumes: `PropositionListEntry`.
- Produces:
  ```typescript
  export interface NamespaceNode {
    segment: string;        // this node's own name segment
    path: string;           // dotted path from the root to here
    children: NamespaceNode[];
    entry?: PropositionListEntry;   // present when a proposition lives at this exact path
  }
  export function buildNamespaceTree(entries: PropositionListEntry[]): NamespaceNode[];
  export function filterTree(nodes: NamespaceNode[], query: string, models?: string[]): NamespaceNode[];
  export function countLeaves(nodes: NamespaceNode[]): number;
  ```

- [ ] **Step 1: Write the failing tests**

Create `ui/packages/rules-core/test/namespaceTree.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { buildNamespaceTree, filterTree, countLeaves } from '../src/namespaceTree.js';
import type { PropositionListEntry } from '../src/contracts.js';

function entry(name: string, modelType = 'customer'): PropositionListEntry {
  return {
    name, modelType, metadataType: 'String', isAsync: false,
    origin: 'Authored', version: 1, description: null, quarantine: [],
  };
}

describe('buildNamespaceTree', () => {
  it('puts an undotted name at the root', () => {
    const tree = buildNamespaceTree([entry('is-active')]);

    expect(tree).toHaveLength(1);
    expect(tree[0]!.segment).toBe('is-active');
    expect(tree[0]!.path).toBe('is-active');
    expect(tree[0]!.entry?.name).toBe('is-active');
    expect(tree[0]!.children).toEqual([]);
  });

  it('nests a dotted name under its namespace', () => {
    const tree = buildNamespaceTree([entry('customer.eligibility.is-active')]);

    expect(tree.map((node) => node.segment)).toEqual(['customer']);
    expect(tree[0]!.path).toBe('customer');
    expect(tree[0]!.entry).toBeUndefined();
    const eligibility = tree[0]!.children[0]!;
    expect(eligibility.segment).toBe('eligibility');
    expect(eligibility.path).toBe('customer.eligibility');
    expect(eligibility.children[0]!.path).toBe('customer.eligibility.is-active');
  });

  it('shares a namespace between siblings', () => {
    const tree = buildNamespaceTree([entry('customer.is-active'), entry('customer.is-adult')]);

    expect(tree).toHaveLength(1);
    expect(tree[0]!.children.map((node) => node.segment)).toEqual(['is-active', 'is-adult']);
  });

  it('sorts namespaces before leaves, each alphabetically', () => {
    const tree = buildNamespaceTree([
      entry('customer.zeta'),
      entry('customer.alpha'),
      entry('customer.nested.thing'),
    ]);

    expect(tree[0]!.children.map((node) => node.segment)).toEqual(['nested', 'alpha', 'zeta']);
  });

  it('lets a namespace also be a proposition in its own right', () => {
    // `customer` is both a name and a namespace — the tree must carry the entry and the children
    const tree = buildNamespaceTree([entry('customer'), entry('customer.is-active')]);

    expect(tree).toHaveLength(1);
    expect(tree[0]!.entry?.name).toBe('customer');
    expect(tree[0]!.children).toHaveLength(1);
  });

  it('returns nothing for no entries', () => {
    expect(buildNamespaceTree([])).toEqual([]);
  });
});

describe('filterTree', () => {
  const tree = buildNamespaceTree([
    entry('customer.eligibility.is-active'),
    entry('customer.eligibility.is-adult'),
    entry('customer.risk.is-fraudulent'),
    entry('order.is-large', 'order'),
  ]);

  it('returns the whole tree for an empty query', () => {
    expect(countLeaves(filterTree(tree, ''))).toBe(4);
  });

  it('keeps only matching leaves, with their ancestors', () => {
    const filtered = filterTree(tree, 'fraud');

    expect(countLeaves(filtered)).toBe(1);
    expect(filtered[0]!.segment).toBe('customer');
    expect(filtered[0]!.children[0]!.segment).toBe('risk');
    expect(filtered[0]!.children[0]!.children[0]!.path).toBe('customer.risk.is-fraudulent');
  });

  it('matches against the full dotted path, not just the leaf segment', () => {
    // Searching by namespace is the main way a big catalog gets navigated
    expect(countLeaves(filterTree(tree, 'eligibility'))).toBe(2);
  });

  it('matches case-insensitively', () => {
    expect(countLeaves(filterTree(tree, 'FRAUD'))).toBe(1);
  });

  it('returns nothing when nothing matches', () => {
    expect(filterTree(tree, 'nonexistent')).toEqual([]);
  });

  it('filters by model type', () => {
    const filtered = filterTree(tree, '', ['order']);

    expect(countLeaves(filtered)).toBe(1);
    expect(filtered[0]!.children[0]!.path).toBe('order.is-large');
  });

  it('combines a query with a model filter', () => {
    expect(countLeaves(filterTree(tree, 'is-', ['order']))).toBe(1);
  });

  it('treats an empty model list as no model filter', () => {
    expect(countLeaves(filterTree(tree, '', []))).toBe(4);
  });

  it('drops a namespace whose only matching descendant was filtered out by model', () => {
    expect(filterTree(tree, 'fraud', ['order'])).toEqual([]);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd ui && pnpm --filter @motiv/rules-core test
```

Expected: FAIL — cannot resolve `../src/namespaceTree.js`.

- [ ] **Step 3: Write the implementation**

Create `ui/packages/rules-core/src/namespaceTree.ts`:

```typescript
import type { PropositionListEntry } from './contracts.js';

/**
 * A node in the namespace tree. The tree is a pure projection of the dotted names — there is no
 * stored hierarchy to keep in sync, so a rename is a move and nothing else has to know.
 */
export interface NamespaceNode {
  /** This node's own name segment. */
  segment: string;
  /** The dotted path from the root to here. */
  path: string;
  children: NamespaceNode[];
  /**
   * The proposition living at exactly this path, when there is one. A node can have both an entry
   * and children: `customer` may be a proposition *and* a namespace.
   */
  entry?: PropositionListEntry;
}

/** Builds the namespace tree from a flat listing, sorting namespaces before leaves. */
export function buildNamespaceTree(entries: PropositionListEntry[]): NamespaceNode[] {
  const roots: NamespaceNode[] = [];

  for (const entry of entries) {
    const segments = entry.name.split('.');
    let siblings = roots;
    let path = '';

    for (const [index, segment] of segments.entries()) {
      path = path === '' ? segment : `${path}.${segment}`;
      let node = siblings.find((candidate) => candidate.segment === segment);
      if (!node) {
        node = { segment, path, children: [] };
        siblings.push(node);
      }
      if (index === segments.length - 1) node.entry = entry;
      siblings = node.children;
    }
  }

  return sort(roots);
}

/**
 * Narrows the tree to nodes matching `query` (substring of the full dotted path, case-insensitive)
 * and, when `models` is non-empty, to leaves of those model types. A matching leaf keeps its
 * ancestors so its position stays legible; a namespace with no surviving descendant is dropped.
 */
export function filterTree(
  nodes: NamespaceNode[], query: string, models: string[] = [],
): NamespaceNode[] {
  const needle = query.trim().toLowerCase();
  if (needle === '' && models.length === 0) return nodes;

  const kept: NamespaceNode[] = [];

  for (const node of nodes) {
    const children = filterTree(node.children, query, models);
    const selfMatches = node.entry !== undefined
      && node.path.toLowerCase().includes(needle)
      && (models.length === 0 || models.includes(node.entry.modelType));

    // A namespace survives only for the sake of a descendant that matched.
    if (!selfMatches && children.length === 0) continue;

    kept.push({
      segment: node.segment,
      path: node.path,
      children,
      ...(selfMatches && node.entry ? { entry: node.entry } : {}),
    });
  }

  return kept;
}

/** How many propositions the tree holds — what a "N matches" count reports. */
export function countLeaves(nodes: NamespaceNode[]): number {
  return nodes.reduce(
    (total, node) => total + (node.entry ? 1 : 0) + countLeaves(node.children),
    0,
  );
}

/** Namespaces first, then leaves, each group alphabetical — depth reads before detail. */
function sort(nodes: NamespaceNode[]): NamespaceNode[] {
  for (const node of nodes) sort(node.children);
  nodes.sort((left, right) => {
    const leftIsNamespace = left.children.length > 0;
    const rightIsNamespace = right.children.length > 0;
    if (leftIsNamespace !== rightIsNamespace) return leftIsNamespace ? -1 : 1;
    return left.segment.localeCompare(right.segment);
  });
  return nodes;
}
```

- [ ] **Step 4: Export it**

Add to `ui/packages/rules-core/src/index.ts`:

```typescript
export { buildNamespaceTree, filterTree, countLeaves } from './namespaceTree.js';
export type { NamespaceNode } from './namespaceTree.js';
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
cd ui && pnpm --filter @motiv/rules-core test && pnpm --filter @motiv/rules-core exec tsc --noEmit
```

Expected: PASS, seventeen new tests.

If `drops a namespace whose only matching descendant was filtered out by model` fails, check that a node with children but no surviving descendants is skipped *before* being pushed — an entry-less namespace must never survive on its own.

- [ ] **Step 6: Commit**

```bash
git add ui/packages/rules-core/src ui/packages/rules-core/test/namespaceTree.test.ts
git commit -m "feat(rules-core): project dotted names into a searchable namespace tree"
```

---

## Phase 5 — The demo UI

### Task 18: Hash routing and the page shell

Behaviour-preserving refactor plus routing. The Rules page must look and behave exactly as it does today when this task ends.

**Files:**
- Create: `ui/apps/demo/src/routing/useHashRoute.ts`
- Create: `ui/apps/demo/src/panes/AppBar.tsx`
- Create: `ui/apps/demo/src/panes/RulesPage.tsx`
- Modify: `ui/apps/demo/src/App.tsx`
- Modify: `ui/apps/demo/src/panes/RuleHeader.tsx`
- Modify: `ui/apps/demo/src/styles/app.css`
- Test: `ui/apps/demo/test/routing/useHashRoute.test.ts` (create), `ui/apps/demo/test/App.test.tsx`

**Interfaces:**
- Consumes: nothing new.
- Produces:
  ```typescript
  // routing/useHashRoute.ts
  export type Page = 'rules' | 'propositions';
  export interface Route { page: Page; name: string | null }
  export function parseHash(hash: string): Route;
  export function formatHash(route: Route): string;
  export function useHashRoute(): [Route, (route: Route) => void];

  // panes/AppBar.tsx
  export function AppBar(props: { page: Page; onNavigate: (page: Page) => void; controls?: ReactNode; children?: ReactNode }): JSX.Element;

  // panes/RulesPage.tsx
  export function RulesPage(props: { client: RulesApiClient; page: Page; onNavigate: (page: Page) => void }): JSX.Element;
  ```

- [ ] **Step 1: Write the failing routing tests**

Create `ui/apps/demo/test/routing/useHashRoute.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { parseHash, formatHash } from '../../src/routing/useHashRoute.js';

describe('parseHash', () => {
  it('defaults to the rules page with no selection', () => {
    expect(parseHash('')).toEqual({ page: 'rules', name: null });
    expect(parseHash('#')).toEqual({ page: 'rules', name: null });
    expect(parseHash('#/')).toEqual({ page: 'rules', name: null });
  });

  it('reads a rule route', () => {
    expect(parseHash('#/rules/can-checkout')).toEqual({ page: 'rules', name: 'can-checkout' });
  });

  it('reads a proposition route with a dotted name', () => {
    expect(parseHash('#/propositions/customer.eligibility.is-active'))
      .toEqual({ page: 'propositions', name: 'customer.eligibility.is-active' });
  });

  it('reads a page with no selection', () => {
    expect(parseHash('#/propositions')).toEqual({ page: 'propositions', name: null });
  });

  it('decodes a percent-encoded name', () => {
    expect(parseHash('#/rules/a%20b')).toEqual({ page: 'rules', name: 'a b' });
  });

  it('falls back to rules for an unknown page', () => {
    expect(parseHash('#/nonsense/x')).toEqual({ page: 'rules', name: null });
  });
});

describe('formatHash', () => {
  it('formats a page with no selection', () => {
    expect(formatHash({ page: 'propositions', name: null })).toBe('#/propositions');
  });

  it('formats a selection', () => {
    expect(formatHash({ page: 'rules', name: 'can-checkout' })).toBe('#/rules/can-checkout');
  });

  it('leaves dots unescaped so the hash stays readable', () => {
    expect(formatHash({ page: 'propositions', name: 'customer.is-active' }))
      .toBe('#/propositions/customer.is-active');
  });

  it('round-trips every route it formats', () => {
    for (const route of [
      { page: 'rules', name: null },
      { page: 'propositions', name: 'customer.a.b' },
      { page: 'rules', name: 'can-checkout' },
    ] as const) {
      expect(parseHash(formatHash(route))).toEqual(route);
    }
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd ui && pnpm --filter @motiv/rules-demo test
```

Expected: FAIL — cannot resolve `../../src/routing/useHashRoute.js`.

- [ ] **Step 3: Write the router**

Create `ui/apps/demo/src/routing/useHashRoute.ts`:

```typescript
import { useCallback, useEffect, useState } from 'react';

/** The two pages the demo shell switches between. */
export type Page = 'rules' | 'propositions';

/** Where the user is: which page, and what is selected on it. */
export interface Route {
  page: Page;
  name: string | null;
}

const PAGES: readonly Page[] = ['rules', 'propositions'];
const DEFAULT_ROUTE: Route = { page: 'rules', name: null };

/**
 * Reads a route out of a location hash. Hash routing rather than history routing so a fork needs no
 * server-side fallback to make deep links work — the demo's host happens to have one, but the
 * skeleton should not depend on it.
 */
export function parseHash(hash: string): Route {
  const [page, ...rest] = hash.replace(/^#\/?/, '').split('/');
  if (!page || !PAGES.includes(page as Page)) return DEFAULT_ROUTE;
  const name = rest.join('/');
  return { page: page as Page, name: name === '' ? null : decodeURIComponent(name) };
}

/** The hash for a route. Dots are left unescaped, so a namespaced name stays readable in the bar. */
export function formatHash(route: Route): string {
  return route.name === null
    ? `#/${route.page}`
    : `#/${route.page}/${encodeURIComponent(route.name)}`;
}

/**
 * The current route, and a setter that writes it to the address bar. Listens on `hashchange`, so the
 * back button and a hand-edited URL both work without a router dependency.
 */
export function useHashRoute(): [Route, (route: Route) => void] {
  const [route, setRoute] = useState<Route>(() => parseHash(window.location.hash));

  useEffect(() => {
    const onHashChange = (): void => setRoute(parseHash(window.location.hash));
    window.addEventListener('hashchange', onHashChange);
    return () => window.removeEventListener('hashchange', onHashChange);
  }, []);

  // Writing the hash fires `hashchange`, which is what actually updates the state — so the address
  // bar stays the single source of truth and a manual edit behaves identically to a click.
  const navigate = useCallback((next: Route): void => {
    window.location.hash = formatHash(next);
  }, []);

  return [route, navigate];
}
```

- [ ] **Step 4: Extract `AppBar`**

Create `ui/apps/demo/src/panes/AppBar.tsx`:

```typescript
import type { ReactNode } from 'react';
import type { Page } from '../routing/useHashRoute.js';

/** The pages, in the order they are offered. */
const PAGES: ReadonlyArray<{ id: Page; label: string }> = [
  { id: 'rules', label: 'Rules' },
  { id: 'propositions', label: 'Propositions' },
];

/**
 * The shell's top bar: brand, page tabs, then whatever breadcrumb trail the current page supplies,
 * and its controls on the right. Extracted from RuleHeader so both pages share one chrome rather
 * than growing two that drift apart.
 */
export function AppBar(props: {
  page: Page;
  onNavigate: (page: Page) => void;
  controls?: ReactNode;
  children?: ReactNode;
}) {
  return (
    <header className="appbar">
      <div className="appbar-brand">
        <span className="appbar-mark" aria-hidden="true">M</span>
        <span className="appbar-wordmark">Motiv</span>
      </div>
      <div className="page-tabs" role="tablist" aria-label="Page">
        {PAGES.map(({ id, label }) => (
          <button
            key={id}
            type="button"
            role="tab"
            aria-selected={props.page === id}
            className={props.page === id ? 'tab active' : 'tab'}
            onClick={() => props.onNavigate(id)}
          >
            {label}
          </button>
        ))}
      </div>
      {props.children}
      <div className="appbar-fill" />
      <div className="appbar-controls">{props.controls}</div>
    </header>
  );
}
```

- [ ] **Step 5: Rebuild `RuleHeader` on top of it**

In `ui/apps/demo/src/panes/RuleHeader.tsx`, keep every piece of state and both `load`/`save` exactly as they are. Replace only the returned markup (lines 75-121) with:

```typescript
  return (
    <>
      <AppBar
        page={props.page}
        onNavigate={props.onNavigate}
        controls={
          <>
            {loaded && (
              <span className="rule-version">
                v{loaded.version}
                {loaded.isCodeDefault && <em> — code-defined default (builder starts fresh)</em>}
              </span>
            )}
            <button type="button" className="btn" disabled={!loaded || saving} onClick={() => void save()}>
              Save
            </button>
          </>
        }
      >
        <span className="breadcrumb-sep">/</span>
        <span className="breadcrumb-item">Eligibility rules</span>
        <span className="breadcrumb-sep">/</span>
        {/* The trail's leaf is the rule picker: the crumb already names the rule in force, so a
            separate control alongside it would be the same fact stated twice. */}
        <ListboxPicker
          options={options}
          value={loaded?.name ?? LOCAL_DRAFT.value}
          onChoose={(name) => void load(name)}
          open={picking}
          setOpen={setPicking}
          triggerName="rule"
          listLabel="rules"
          triggerClassName="breadcrumb-current"
          listClassName="breadcrumb-menu"
        />
        <span className="model-pill" title="Model type the rule is validated and evaluated against">
          {MODEL_TYPE}
        </span>
      </AppBar>
      {conflict !== null && loaded && (
        <div role="alert" className="conflict-banner">
          Someone else saved version {conflict} of “{loaded.name}”.
          <button type="button" className="btn" onClick={() => void load(loaded.name)}>
            Reload latest
          </button>
        </div>
      )}
    </>
  );
```

Widen its props to `{ client: RulesApiClient; page: Page; onNavigate: (page: Page) => void }` and add the imports for `AppBar` and `Page`.

- [ ] **Step 6: Extract `RulesPage` and route in `App`**

Create `ui/apps/demo/src/panes/RulesPage.tsx`:

```typescript
import type { RulesApiClient } from '@motiv/rules-core';
import type { Page } from '../routing/useHashRoute.js';
import { RuleHeader } from './RuleHeader.js';
import { EditorPane } from './EditorPane.js';
import { JsonPane } from './JsonPane.js';
import { EvaluatePane } from './EvaluatePane.js';
import { CheckoutPane } from './CheckoutPane.js';

/** The rules page: today's shell, unchanged, now behind a route. */
export function RulesPage(props: {
  client: RulesApiClient;
  page: Page;
  onNavigate: (page: Page) => void;
}) {
  return (
    <>
      <RuleHeader client={props.client} page={props.page} onNavigate={props.onNavigate} />
      {/*
        Each pane below fetches GET /catalog on mount (EditorPane and EvaluatePane
        via useCatalog, CheckoutPane directly) — and EditorPane's builder surface
        fetches once more of its own, so up to four requests for the same static
        payload. Deduping would mean lifting the catalog here and passing it down,
        but each pane's self-contained wiring is a deliberate seam this demo exists
        to show, so the duplicate requests are accepted.
      */}
      <div className="shell-body">
        <EditorPane client={props.client} />
        <JsonPane />
        <EvaluatePane client={props.client} />
      </div>
      <CheckoutPane client={props.client} />
    </>
  );
}
```

Then replace the returned markup of `ui/apps/demo/src/App.tsx` (lines 33-55) with:

```typescript
  const [route, navigate] = useHashRoute();

  return (
    // Seam: the store hookup. RuleEditorProvider exposes the single RuleEditorStore
    // to every builder component (useRuleEditorStore / useRuleNode) below it.
    <RuleEditorProvider store={store}>
      <main className="app">
        {route.page === 'propositions'
          ? (
            <PropositionsPage
              client={client}
              page={route.page}
              selected={route.name}
              onNavigate={(page) => navigate({ page, name: null })}
              onSelect={(name) => navigate({ page: 'propositions', name })}
            />
          )
          : <RulesPage client={client} page={route.page} onNavigate={(page) => navigate({ page, name: null })} />}
      </main>
    </RuleEditorProvider>
  );
```

`PropositionsPage` arrives in Task 20. **Until then**, render `<RulesPage …/>` for both branches so the app compiles and this task's tests can pass — replace the placeholder in Task 20 Step 5.

- [ ] **Step 7: Style the page tabs**

Append to `ui/apps/demo/src/styles/app.css`, in the appbar section near `.appbar-brand` (around line 69):

```css
/* Page tabs: the Rules/Propositions switch in the appbar. Reuses `.tab` from the surface tabs so
   both switches read as the same control at two altitudes. */
.page-tabs {
  display: flex;
  gap: 2px;
  margin-left: var(--space-3);
  padding: 2px;
  border-radius: var(--radius-2);
  background: var(--sh-inset);
}
```

If `--sh-inset` or `--space-3` are not defined in `tokens.css`, substitute the nearest existing token — check `tokens.css` rather than inventing names.

- [ ] **Step 8: Add a routing test to the app test**

Append to `ui/apps/demo/test/App.test.tsx`, following the file's existing render helpers:

```typescript
  it('shows the rules page by default', async () => {
    window.location.hash = '';
    renderApp();

    expect(await screen.findByRole('tab', { name: 'Rules', selected: true })).toBeTruthy();
  });

  it('switches page when a tab is clicked', async () => {
    window.location.hash = '';
    renderApp();

    await userEvent.click(await screen.findByRole('tab', { name: 'Propositions' }));

    expect(window.location.hash).toBe('#/propositions');
  });
```

- [ ] **Step 9: Run the demo tests and typecheck**

```bash
cd ui && pnpm --filter @motiv/rules-demo test && pnpm --filter @motiv/rules-demo typecheck
```

Expected: PASS. Every pre-existing demo test must still pass — this task changes structure, not behaviour. A failing pane test means the extraction dropped a prop.

- [ ] **Step 10: Commit**

```bash
git add ui/apps/demo/src ui/apps/demo/test
git commit -m "feat(demo): route between pages on the hash and share one appbar"
```

---

### Task 19: The proposition explorer

**Files:**
- Create: `ui/apps/demo/src/explorer/PropositionExplorer.tsx`
- Modify: `ui/apps/demo/src/styles/app.css`
- Test: `ui/apps/demo/test/explorer/PropositionExplorer.test.tsx` (create)

**Interfaces:**
- Consumes: `buildNamespaceTree`, `filterTree`, `countLeaves`, `NamespaceNode`, `PropositionListEntry`.
- Produces:
  ```typescript
  export interface ExplorerActions {
    onSelect: (name: string) => void;
    onDerive: (name: string) => void;
    onNew: () => void;
    onDelete: (entry: PropositionListEntry) => void;
  }
  export function PropositionExplorer(props: {
    entries: PropositionListEntry[];
    selected: string | null;
    actions: ExplorerActions;
  }): JSX.Element;
  ```

- [ ] **Step 1: Write the failing tests**

Create `ui/apps/demo/test/explorer/PropositionExplorer.test.tsx`:

```typescript
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { PropositionListEntry } from '@motiv/rules-core';
import { PropositionExplorer } from '../../src/explorer/PropositionExplorer.js';

function entry(overrides: Partial<PropositionListEntry> & { name: string }): PropositionListEntry {
  return {
    modelType: 'customer', metadataType: 'String', isAsync: false,
    origin: 'Authored', version: 1, description: null, quarantine: [],
    ...overrides,
  };
}

const ENTRIES = [
  entry({ name: 'customer.eligibility.is-active', origin: 'Compiled', version: 0 }),
  entry({ name: 'customer.eligibility.is-adult' }),
  entry({ name: 'customer.risk.is-fraudulent' }),
  entry({ name: 'order.is-large', modelType: 'order' }),
];

function renderExplorer(overrides: Partial<Parameters<typeof PropositionExplorer>[0]> = {}) {
  const actions = { onSelect: vi.fn(), onDerive: vi.fn(), onNew: vi.fn(), onDelete: vi.fn() };
  render(
    <PropositionExplorer entries={ENTRIES} selected={null} actions={actions} {...overrides} />,
  );
  return actions;
}

describe('PropositionExplorer', () => {
  it('renders every proposition as a leaf', () => {
    renderExplorer();

    expect(screen.getByRole('treeitem', { name: /is-active/ })).toBeTruthy();
    expect(screen.getByRole('treeitem', { name: /is-large/ })).toBeTruthy();
  });

  it('groups leaves under their namespace', () => {
    renderExplorer();

    expect(screen.getByRole('treeitem', { name: /^customer/ })).toBeTruthy();
    expect(screen.getByRole('treeitem', { name: /^order/ })).toBeTruthy();
  });

  it('filters as you type, matching the full dotted path', async () => {
    renderExplorer();

    await userEvent.type(screen.getByRole('searchbox', { name: /filter/i }), 'fraud');

    expect(screen.queryByRole('treeitem', { name: /is-fraudulent/ })).toBeTruthy();
    expect(screen.queryByRole('treeitem', { name: /is-adult/ })).toBeNull();
  });

  it('reports how many propositions match', async () => {
    renderExplorer();

    await userEvent.type(screen.getByRole('searchbox', { name: /filter/i }), 'eligibility');

    expect(screen.getByText(/2 of 4/)).toBeTruthy();
  });

  it('narrows to one model when a chip is toggled', async () => {
    renderExplorer();

    await userEvent.click(screen.getByRole('button', { name: 'order' }));

    expect(screen.queryByRole('treeitem', { name: /is-large/ })).toBeTruthy();
    expect(screen.queryByRole('treeitem', { name: /is-adult/ })).toBeNull();
  });

  it('selects a proposition when its leaf is clicked', async () => {
    const actions = renderExplorer();

    await userEvent.click(screen.getByRole('treeitem', { name: /is-fraudulent/ }));

    expect(actions.onSelect).toHaveBeenCalledWith('customer.risk.is-fraudulent');
  });

  it('does not select a namespace that holds no proposition', async () => {
    const actions = renderExplorer();

    await userEvent.click(screen.getByRole('treeitem', { name: /^customer/ }));

    expect(actions.onSelect).not.toHaveBeenCalled();
  });

  it('marks the selected leaf', () => {
    renderExplorer({ selected: 'customer.risk.is-fraudulent' });

    expect(screen.getByRole('treeitem', { name: /is-fraudulent/ }).getAttribute('aria-selected'))
      .toBe('true');
  });

  it('badges an origin on each leaf', () => {
    renderExplorer();

    expect(screen.getByRole('treeitem', { name: /is-active/ }).textContent).toContain('compiled');
    expect(screen.getByRole('treeitem', { name: /is-adult/ }).textContent).toContain('authored');
  });

  it('shows the model type as a pill', () => {
    renderExplorer();

    expect(screen.getByRole('treeitem', { name: /is-large/ }).textContent).toContain('order');
  });

  it('marks a quarantined proposition and shows why', () => {
    renderExplorer({
      entries: [entry({
        name: 'customer.broken',
        quarantine: [{ path: '$', code: 'UnknownSpec', message: 'unknown spec' }],
      })],
    });

    const leaf = screen.getByRole('treeitem', { name: /broken/ });
    expect(leaf.textContent).toContain('quarantined');
    expect(leaf.getAttribute('title')).toContain('unknown spec');
  });

  it('keeps quarantine distinct from origin', () => {
    // Quarantine is orthogonal, not a fourth origin — both marks must show
    renderExplorer({
      entries: [entry({
        name: 'customer.eligibility.is-active',
        origin: 'Overridden',
        quarantine: [{ path: '$', code: 'UnknownSpec', message: 'gone' }],
      })],
    });

    const leaf = screen.getByRole('treeitem', { name: /is-active/ });
    expect(leaf.textContent).toContain('overridden');
    expect(leaf.textContent).toContain('quarantined');
  });

  it('derives from a leaf', async () => {
    const actions = renderExplorer({ selected: 'customer.risk.is-fraudulent' });

    await userEvent.click(screen.getByRole('button', { name: /derive/i }));

    expect(actions.onDerive).toHaveBeenCalledWith('customer.risk.is-fraudulent');
  });

  it('starts a new proposition', async () => {
    const actions = renderExplorer();

    await userEvent.click(screen.getByRole('button', { name: /^new/i }));

    expect(actions.onNew).toHaveBeenCalled();
  });

  it('says so when nothing matches', async () => {
    renderExplorer();

    await userEvent.type(screen.getByRole('searchbox', { name: /filter/i }), 'zzz');

    expect(screen.getByText(/no propositions match/i)).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd ui && pnpm --filter @motiv/rules-demo test
```

Expected: FAIL — cannot resolve `PropositionExplorer.js`.

- [ ] **Step 3: Write the explorer**

Create `ui/apps/demo/src/explorer/PropositionExplorer.tsx`:

```typescript
import { useMemo, useState, type CSSProperties } from 'react';
import {
  buildNamespaceTree, countLeaves, filterTree,
  type NamespaceNode, type PropositionListEntry,
} from '@motiv/rules-core';

/** What the explorer can ask the page to do. */
export interface ExplorerActions {
  onSelect: (name: string) => void;
  onDerive: (name: string) => void;
  onNew: () => void;
  onDelete: (entry: PropositionListEntry) => void;
}

const ORIGIN_LABEL: Record<PropositionListEntry['origin'], string> = {
  Compiled: 'compiled',
  Overridden: 'overridden',
  Authored: 'authored',
};

/**
 * The namespaced tree rail. The hierarchy is a pure projection of the dotted names — there is no
 * stored folder structure — so a rename moves a proposition and nothing else needs to know.
 */
export function PropositionExplorer(props: {
  entries: PropositionListEntry[];
  selected: string | null;
  actions: ExplorerActions;
}) {
  const [query, setQuery] = useState('');
  const [models, setModels] = useState<string[]>([]);

  const tree = useMemo(() => buildNamespaceTree(props.entries), [props.entries]);
  const filtered = useMemo(() => filterTree(tree, query, models), [tree, query, models]);
  const total = props.entries.length;
  const shown = countLeaves(filtered);

  const modelTypes = useMemo(
    () => [...new Set(props.entries.map((entry) => entry.modelType))].sort(),
    [props.entries],
  );

  const selectedEntry = props.entries.find((entry) => entry.name === props.selected);

  const toggleModel = (model: string): void =>
    setModels((current) =>
      current.includes(model) ? current.filter((kept) => kept !== model) : [...current, model]);

  return (
    <aside className="explorer" aria-label="Propositions">
      <div className="explorer-header">
        <input
          type="search"
          className="explorer-search"
          aria-label="Filter propositions"
          placeholder="Filter…"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
        />
        <button type="button" className="btn" onClick={props.actions.onNew}>New</button>
      </div>

      <div className="explorer-chips">
        {modelTypes.map((model) => (
          <button
            key={model}
            type="button"
            className={models.includes(model) ? 'model-pill active' : 'model-pill'}
            aria-pressed={models.includes(model)}
            onClick={() => toggleModel(model)}
          >
            {model}
          </button>
        ))}
        <span className="explorer-count">{shown} of {total}</span>
      </div>

      {shown === 0
        ? <p className="explorer-empty">No propositions match “{query}”.</p>
        : (
          <ul className="explorer-tree" role="tree" aria-label="Proposition namespaces">
            {filtered.map((node) => (
              <TreeNode
                key={node.path}
                node={node}
                depth={0}
                selected={props.selected}
                onSelect={props.actions.onSelect}
              />
            ))}
          </ul>
        )}

      {selectedEntry && (
        <div className="explorer-actions">
          <button type="button" className="btn" onClick={() => props.actions.onDerive(selectedEntry.name)}>
            Derive from this
          </button>
          {selectedEntry.origin !== 'Compiled' && (
            <button type="button" className="btn" onClick={() => props.actions.onDelete(selectedEntry)}>
              {selectedEntry.origin === 'Overridden' ? 'Revert to compiled' : 'Delete'}
            </button>
          )}
        </div>
      )}
    </aside>
  );
}

/**
 * One node. A node can be both a namespace and a proposition — `customer` may be a name in its own
 * right — so the entry and the children are rendered independently rather than as an either/or.
 */
function TreeNode(props: {
  node: NamespaceNode;
  depth: number;
  selected: string | null;
  onSelect: (name: string) => void;
}) {
  const { node } = props;
  const entry = node.entry;
  const quarantined = (entry?.quarantine.length ?? 0) > 0;

  return (
    <li
      role="treeitem"
      aria-selected={entry !== undefined && entry.name === props.selected}
      aria-expanded={node.children.length > 0 ? true : undefined}
      className={quarantined ? 'explorer-node quarantined' : 'explorer-node'}
      style={{ '--depth': props.depth } as CSSProperties}
      title={quarantined ? entry!.quarantine.map((error) => error.message).join('\n') : undefined}
    >
      <span
        className={entry ? 'explorer-leaf' : 'explorer-namespace'}
        // Only a node that *is* a proposition is selectable; a bare namespace is scaffolding.
        onClick={entry ? () => props.onSelect(entry.name) : undefined}
      >
        <span className="explorer-segment">{node.segment}</span>
        {entry && (
          <>
            <span className="model-pill">{entry.modelType}</span>
            <span className="origin-badge">{ORIGIN_LABEL[entry.origin]}</span>
            {quarantined && <span className="quarantine-badge">quarantined</span>}
          </>
        )}
      </span>

      {node.children.length > 0 && (
        <ul role="group">
          {node.children.map((child) => (
            <TreeNode
              key={child.path}
              node={child}
              depth={props.depth + 1}
              selected={props.selected}
              onSelect={props.onSelect}
            />
          ))}
        </ul>
      )}
    </li>
  );
}
```

- [ ] **Step 4: Style it**

Append to `ui/apps/demo/src/styles/app.css`:

```css
/* ---------------------------------------------------------------------------
   Proposition explorer — the namespaced tree rail on the propositions page.
   Indentation is driven by a --depth custom property set per node, so nesting
   needs no per-level selectors.
   --------------------------------------------------------------------------- */
.explorer {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  min-width: 0;
  padding: var(--space-3);
  overflow-y: auto;
  border-right: 1px solid var(--sh-border);
}

.explorer-header {
  display: flex;
  gap: var(--space-2);
}

.explorer-search {
  flex: 1;
  min-width: 0;
}

.explorer-chips {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-1);
}

.explorer-count {
  margin-left: auto;
  font-size: 0.75rem;
  opacity: 0.7;
}

.explorer-tree,
.explorer-tree ul {
  margin: 0;
  padding: 0;
  list-style: none;
}

.explorer-node {
  --depth: 0;
}

.explorer-leaf,
.explorer-namespace {
  display: flex;
  align-items: center;
  gap: var(--space-1);
  padding: 2px var(--space-1);
  padding-left: calc(var(--space-1) + var(--depth) * var(--space-3));
  border-radius: var(--radius-1);
}

.explorer-leaf {
  cursor: pointer;
}

.explorer-leaf:hover {
  background: var(--sh-hover);
}

.explorer-namespace {
  font-weight: 600;
  opacity: 0.8;
}

.explorer-node[aria-selected='true'] > .explorer-leaf {
  background: var(--sh-selected);
}

.origin-badge,
.quarantine-badge {
  font-size: 0.7rem;
  padding: 0 4px;
  border-radius: var(--radius-1);
  opacity: 0.75;
}

/* Quarantine reads as a warning and stacks with the origin badge rather than replacing it —
   an overridden proposition can also be quarantined. */
.quarantine-badge {
  background: var(--sh-warning, #fde68a);
  color: #713f12;
  opacity: 1;
}

.explorer-empty {
  font-size: 0.85rem;
  opacity: 0.7;
}

.explorer-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  margin-top: auto;
  padding-top: var(--space-2);
  border-top: 1px solid var(--sh-border);
}

/* The propositions page adds a rail ahead of the three panes. */
.shell-body.with-rail {
  grid-template-columns: minmax(14rem, 20rem) repeat(3, minmax(0, 1fr));
}

@media (max-width: 60rem) {
  .shell-body.with-rail {
    grid-template-columns: minmax(0, 1fr);
  }
}
```

**Before running:** every `var(--…)` above must exist. Read `ui/apps/demo/src/styles/tokens.css` and substitute the project's real token names for any that do not (`--sh-hover`, `--sh-selected`, `--sh-inset`, `--sh-warning`, `--space-*`, `--radius-*` are guesses). Do not add new tokens unless the file has no equivalent.

- [ ] **Step 5: Run tests to verify they pass**

```bash
cd ui && pnpm --filter @motiv/rules-demo test && pnpm --filter @motiv/rules-demo typecheck
```

Expected: PASS, fifteen tests.

If `does not select a namespace that holds no proposition` fails, the click handler is attached unconditionally — it must be `undefined` for an entry-less node, not a no-op function, so no handler runs at all.

- [ ] **Step 6: Commit**

```bash
git add ui/apps/demo/src/explorer ui/apps/demo/src/styles/app.css ui/apps/demo/test/explorer
git commit -m "feat(demo): searchable namespaced proposition explorer"
```

---

### Task 20: The propositions page

**Files:**
- Create: `ui/apps/demo/src/explorer/PropositionDialog.tsx`
- Create: `ui/apps/demo/src/explorer/DependentsStrip.tsx`
- Create: `ui/apps/demo/src/panes/PropositionsPage.tsx`
- Modify: `ui/apps/demo/src/App.tsx` (replace the Task 18 placeholder)
- Modify: `ui/apps/demo/src/styles/app.css`
- Test: `ui/apps/demo/test/panes/PropositionsPage.test.tsx` (create)

**Interfaces:**
- Consumes: `PropositionExplorer`, `ExplorerActions`, the client methods from Task 16, `RuleEditorStore`.
- Produces:
  ```typescript
  export interface DialogSeed { name: string; modelType: string; deriveFrom: string | null }
  export function PropositionDialog(props: { seed: DialogSeed; modelTypes: string[]; onCancel: () => void; onCreate: (values: { name: string; modelType: string; description: string | null }) => void; error: string | null }): JSX.Element;
  export function DependentsStrip(props: { dependents: DependentEntry[] }): JSX.Element | null;
  export function PropositionsPage(props: { client: RulesApiClient; page: Page; selected: string | null; onNavigate: (page: Page) => void; onSelect: (name: string | null) => void }): JSX.Element;
  ```

- [ ] **Step 1: Write the failing tests**

Create `ui/apps/demo/test/panes/PropositionsPage.test.tsx`. Read `ui/apps/demo/test/support/` first and reuse its existing fake-client / store helpers rather than hand-rolling a fetch mock.

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RuleEditorStore, type PropositionListEntry } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { PropositionsPage } from '../../src/panes/PropositionsPage.js';

function entry(overrides: Partial<PropositionListEntry> & { name: string }): PropositionListEntry {
  return {
    modelType: 'customer', metadataType: 'String', isAsync: false,
    origin: 'Authored', version: 1, description: null, quarantine: [],
    ...overrides,
  };
}

/** A client stubbed just far enough for the page: only the calls it actually makes. */
function stubClient(overrides: Record<string, unknown> = {}) {
  return {
    listPropositions: vi.fn().mockResolvedValue([
      entry({ name: 'customer.is-active', origin: 'Compiled', version: 0 }),
      entry({ name: 'customer.derived' }),
    ]),
    getProposition: vi.fn().mockResolvedValue({
      document: { spec: 'customer.is-active' }, version: 1,
      origin: 'Authored', hasCompiledDefault: false,
    }),
    getDependents: vi.fn().mockResolvedValue([]),
    createProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 1 }),
    putProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 2 }),
    deleteProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 0 }),
    getCatalog: vi.fn().mockResolvedValue({ specs: [], collections: [], metadataTypes: {}, modelTypes: {} }),
    validate: vi.fn().mockResolvedValue({ errors: [] }),
    ...overrides,
  };
}

function renderPage(client: ReturnType<typeof stubClient>, selected: string | null = null) {
  const onSelect = vi.fn();
  render(
    <RuleEditorProvider store={new RuleEditorStore({ rule: { spec: 'customer.is-active' } })}>
      <PropositionsPage
        client={client as never}
        page="propositions"
        selected={selected}
        onNavigate={vi.fn()}
        onSelect={onSelect}
      />
    </RuleEditorProvider>,
  );
  return onSelect;
}

describe('PropositionsPage', () => {
  beforeEach(() => { window.location.hash = ''; });

  it('lists propositions in the explorer on mount', async () => {
    const client = stubClient();
    renderPage(client);

    expect(await screen.findByRole('treeitem', { name: /is-active/ })).toBeTruthy();
    expect(client.listPropositions).toHaveBeenCalled();
  });

  it('loads the selected proposition document', async () => {
    const client = stubClient();
    renderPage(client, 'customer.derived');

    await waitFor(() => expect(client.getProposition).toHaveBeenCalledWith('customer.derived'));
  });

  it('shows the selected name as breadcrumb segments', async () => {
    const client = stubClient();
    renderPage(client, 'customer.derived');

    // The dotted name renders as a trail, which is the payoff of namespacing by name.
    // Scoped to the banner: "customer" also appears as a model pill in the explorer, so an
    // unscoped findByText would match several nodes and throw.
    const bar = await screen.findByRole('banner');
    expect(bar.querySelector('.breadcrumb-current')?.textContent).toBe('derived');
    expect([...bar.querySelectorAll('.breadcrumb-item')].map((node) => node.textContent))
      .toContain('customer');
  });

  it('fetches the blast radius for the selection', async () => {
    const client = stubClient({
      getDependents: vi.fn().mockResolvedValue([{ name: 'can-checkout', kind: 'rule' }]),
    });
    renderPage(client, 'customer.derived');

    expect(await screen.findByText(/can-checkout/)).toBeTruthy();
  });

  it('says how many things an edit would affect', async () => {
    const client = stubClient({
      getDependents: vi.fn().mockResolvedValue([
        { name: 'can-checkout', kind: 'rule' },
        { name: 'customer.other', kind: 'proposition' },
      ]),
    });
    renderPage(client, 'customer.derived');

    expect(await screen.findByText(/1 rule and 1 proposition/i)).toBeTruthy();
  });

  it('saves the edited document with the loaded version', async () => {
    const client = stubClient();
    renderPage(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));

    await waitFor(() => expect(client.putProposition)
      .toHaveBeenCalledWith('customer.derived', expect.anything(), 1));
  });

  it('surfaces a conflict when the version was stale', async () => {
    const client = stubClient({
      putProposition: vi.fn().mockResolvedValue({ outcome: 'conflict', currentVersion: 5 }),
    });
    renderPage(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));

    expect(await screen.findByRole('alert')).toBeTruthy();
    expect(screen.getByRole('alert').textContent).toContain('5');
  });

  it('names the rule an edit would break', async () => {
    const client = stubClient({
      putProposition: vi.fn().mockResolvedValue({
        outcome: 'invalid',
        errors: [],
        brokenDependents: [{
          name: 'can-checkout', kind: 'rule',
          errors: [{ path: '$', code: 'AsyncSpecInSyncLoad', message: 'would not bind' }],
        }],
      }),
    });
    renderPage(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));

    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('can-checkout');
    expect(alert.textContent).toContain('would not bind');
  });

  it('creates a proposition from the new dialog', async () => {
    const client = stubClient();
    renderPage(client);
    await screen.findByRole('treeitem', { name: /is-active/ });

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText(/name/i), 'customer.fresh');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      name: 'customer.fresh',
      modelType: 'customer',
    })));
  });

  it('seeds the dialog from the derived-from node', async () => {
    const client = stubClient();
    renderPage(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(screen.getByRole('button', { name: /derive/i }));

    // Prefilled to the source's namespace, so derivation lands beside what it came from
    expect((screen.getByLabelText(/name/i) as HTMLInputElement).value).toBe('customer.');
  });

  it('creates a derived proposition whose document references its source', async () => {
    const client = stubClient();
    renderPage(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(screen.getByRole('button', { name: /derive/i }));
    await userEvent.type(screen.getByLabelText(/name/i), 'customer.onward');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      document: { spec: 'customer.derived' },
    })));
  });

  it('reports a name already taken', async () => {
    const client = stubClient({
      createProposition: vi.fn().mockResolvedValue({ outcome: 'nameTaken' }),
    });
    renderPage(client);
    await screen.findByRole('treeitem', { name: /is-active/ });

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText(/name/i), 'customer.derived');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    expect(await screen.findByText(/already/i)).toBeTruthy();
  });

  it('reports the referrers blocking a delete', async () => {
    const client = stubClient({
      deleteProposition: vi.fn().mockResolvedValue({ outcome: 'referenced', referrers: ['customer.other'] }),
    });
    renderPage(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(screen.getByRole('button', { name: /^delete$/i }));

    expect((await screen.findByRole('alert')).textContent).toContain('customer.other');
  });

  it('refreshes the listing after a successful create', async () => {
    const client = stubClient();
    renderPage(client);
    await screen.findByRole('treeitem', { name: /is-active/ });
    const before = client.listPropositions.mock.calls.length;

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText(/name/i), 'customer.fresh');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    await waitFor(() =>
      expect(client.listPropositions.mock.calls.length).toBeGreaterThan(before));
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd ui && pnpm --filter @motiv/rules-demo test
```

Expected: FAIL — cannot resolve `PropositionsPage.js`.

- [ ] **Step 3: Write the dialog**

Create `ui/apps/demo/src/explorer/PropositionDialog.tsx`:

```typescript
import { useState } from 'react';

/** What the New/Derive flow starts from. */
export interface DialogSeed {
  /** Prefilled name — a trailing dot when deriving, so the namespace is kept and the leaf is typed. */
  name: string;
  modelType: string;
  /** The proposition being derived from, whose reference seeds the new document. */
  deriveFrom: string | null;
}

/**
 * One dialog for both New and Derive. Derivation is a seeded create rather than its own concept, so
 * there is no second persistence shape and no lineage to keep — the reference graph already records
 * exactly what a "derived from" edge would.
 */
export function PropositionDialog(props: {
  seed: DialogSeed;
  modelTypes: string[];
  error: string | null;
  onCancel: () => void;
  onCreate: (values: { name: string; modelType: string; description: string | null }) => void;
}) {
  const [name, setName] = useState(props.seed.name);
  const [modelType, setModelType] = useState(props.seed.modelType);
  const [description, setDescription] = useState('');

  const submit = (): void => props.onCreate({
    name: name.trim(),
    modelType,
    description: description.trim() === '' ? null : description.trim(),
  });

  return (
    <div className="dialog-backdrop" role="presentation">
      <div className="dialog" role="dialog" aria-modal="true" aria-label={
        props.seed.deriveFrom ? `Derive from ${props.seed.deriveFrom}` : 'New proposition'
      }>
        <h2 className="dialog-title">
          {props.seed.deriveFrom ? `Derive from ${props.seed.deriveFrom}` : 'New proposition'}
        </h2>

        <label className="dialog-field">
          <span>Name</span>
          <input
            type="text"
            value={name}
            placeholder="customer.eligibility.is-eligible"
            onChange={(event) => setName(event.target.value)}
            onKeyDown={(event) => { if (event.key === 'Enter' && name.trim() !== '') submit(); }}
          />
          <small>Dots namespace the proposition; each segment starts with a letter.</small>
        </label>

        <label className="dialog-field">
          <span>Model type</span>
          <select value={modelType} onChange={(event) => setModelType(event.target.value)}>
            {props.modelTypes.map((model) => <option key={model} value={model}>{model}</option>)}
          </select>
        </label>

        <label className="dialog-field">
          <span>Description</span>
          <input type="text" value={description} onChange={(event) => setDescription(event.target.value)} />
        </label>

        {props.error !== null && <p className="dialog-error" role="alert">{props.error}</p>}

        <div className="dialog-actions">
          <button type="button" className="btn" onClick={props.onCancel}>Cancel</button>
          <button type="button" className="btn" disabled={name.trim() === ''} onClick={submit}>
            Create
          </button>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Write the dependents strip**

Create `ui/apps/demo/src/explorer/DependentsStrip.tsx`:

```typescript
import type { DependentEntry } from '@motiv/rules-core';

/** "1 rule and 2 propositions", pluralised, omitting a kind with no members. */
function summarise(dependents: DependentEntry[]): string {
  const rules = dependents.filter((dependent) => dependent.kind === 'rule').length;
  const propositions = dependents.length - rules;
  const parts: string[] = [];
  if (rules > 0) parts.push(`${rules} rule${rules === 1 ? '' : 's'}`);
  if (propositions > 0) parts.push(`${propositions} proposition${propositions === 1 ? '' : 's'}`);
  return parts.join(' and ');
}

/**
 * The blast radius, shown while editing rather than sprung at the moment of saving. Who references
 * this proposition is a fact about *other* documents, so it stays accurate as the user types.
 */
export function DependentsStrip(props: { dependents: DependentEntry[] }) {
  if (props.dependents.length === 0) return null;

  return (
    <div className="dependents-strip">
      <strong>Changing this affects {summarise(props.dependents)}:</strong>
      <ul>
        {props.dependents.map((dependent) => (
          <li key={`${dependent.kind}:${dependent.name}`}>
            <span className="origin-badge">{dependent.kind}</span> {dependent.name}
          </li>
        ))}
      </ul>
    </div>
  );
}
```

- [ ] **Step 5: Write the page**

Create `ui/apps/demo/src/panes/PropositionsPage.tsx`:

```typescript
import { useCallback, useEffect, useState } from 'react';
import type {
  DependentEntry, PropositionListEntry, PropositionSaveResult, RulesApiClient,
} from '@motiv/rules-core';
import { useRuleEditor, useRuleEditorStore } from '@motiv/rules-react';
import type { Page } from '../routing/useHashRoute.js';
import { AppBar } from './AppBar.js';
import { EditorPane } from './EditorPane.js';
import { JsonPane } from './JsonPane.js';
import { EvaluatePane } from './EvaluatePane.js';
import { PropositionExplorer } from '../explorer/PropositionExplorer.js';
import { PropositionDialog, type DialogSeed } from '../explorer/PropositionDialog.js';
import { DependentsStrip } from '../explorer/DependentsStrip.js';

/** The loaded proposition's server identity: what Save must send back to avoid clobbering. */
interface Loaded {
  name: string;
  version: number;
  hasCompiledDefault: boolean;
}

/** Renders a save failure as something a person can act on. */
function describeFailure(result: PropositionSaveResult): string | null {
  switch (result.outcome) {
    case 'saved':
      return null;
    case 'conflict':
      return `Someone else saved version ${result.currentVersion}. Reload before saving again.`;
    case 'nameTaken':
      return 'A proposition is already authored under that name.';
    case 'referenced':
      return `Still referenced by ${result.referrers.join(', ')}. Change those first.`;
    case 'invalid': {
      // Broken dependents are reported apart from document errors, because a document error's path
      // points into *this* document and cannot address a break somewhere else.
      const broken = result.brokenDependents.map((dependent) =>
        `${dependent.kind} ${dependent.name} (${dependent.errors.map((error) => error.message).join('; ')})`);
      return broken.length > 0
        ? `This change would break ${broken.join(', ')}.`
        : result.errors.map((error) => error.message).join('; ');
    }
  }
}

/**
 * The propositions page: the namespaced explorer alongside the same Editor / JSON / Evaluate panes
 * the rules page uses. The panes are reused unmodified — they read from the shared RuleEditorStore
 * and never ask what the document represents, so a proposition and a rule are the same thing to them.
 */
export function PropositionsPage(props: {
  client: RulesApiClient;
  page: Page;
  selected: string | null;
  onNavigate: (page: Page) => void;
  onSelect: (name: string | null) => void;
}) {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);
  const [entries, setEntries] = useState<PropositionListEntry[]>([]);
  const [loaded, setLoaded] = useState<Loaded | null>(null);
  const [dependents, setDependents] = useState<DependentEntry[]>([]);
  const [failure, setFailure] = useState<string | null>(null);
  const [dialog, setDialog] = useState<DialogSeed | null>(null);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const refresh = useCallback(async (): Promise<PropositionListEntry[]> => {
    const listed = await props.client.listPropositions();
    setEntries(listed);
    return listed;
  }, [props.client]);

  useEffect(() => { void refresh(); }, [refresh]);

  // Loading is keyed on the route, so a deep link and a click take exactly the same path.
  useEffect(() => {
    let cancelled = false;
    const name = props.selected;

    if (name === null) {
      setLoaded(null);
      setDependents([]);
      return;
    }

    void (async () => {
      const [proposition, affected] = await Promise.all([
        props.client.getProposition(name),
        props.client.getDependents(name),
      ]);
      if (cancelled) return;
      setFailure(null);
      setDependents(affected);
      setLoaded({
        name,
        version: proposition.version,
        hasCompiledDefault: proposition.hasCompiledDefault,
      });
      if (proposition.document) store.loadDocument(proposition.document);
    })();

    return () => { cancelled = true; };
  }, [props.client, props.selected, store]);

  const modelTypes = [...new Set(entries.map((entry) => entry.modelType))].sort();

  const save = async (): Promise<void> => {
    if (!loaded) return;
    setSaving(true);
    try {
      const result = await props.client.putProposition(loaded.name, state.document, loaded.version);
      setFailure(describeFailure(result));
      if (result.outcome === 'saved') {
        setLoaded({ ...loaded, version: result.version });
        await refresh();
      }
    } finally {
      setSaving(false);
    }
  };

  const remove = async (entry: PropositionListEntry): Promise<void> => {
    const result = await props.client.deleteProposition(entry.name, entry.version);
    setFailure(describeFailure(result));
    if (result.outcome !== 'saved') return;
    await refresh();
    // Reverting keeps the name (now compiled); removing does not, so drop the selection.
    props.onSelect(entry.origin === 'Overridden' ? entry.name : null);
  };

  const create = async (values: {
    name: string; modelType: string; description: string | null;
  }): Promise<void> => {
    const document = dialog?.deriveFrom ? { spec: dialog.deriveFrom } : state.document;
    const result = await props.client.createProposition({ ...values, document });

    if (result.outcome !== 'saved') {
      setDialogError(describeFailure(result));
      return;
    }

    setDialog(null);
    setDialogError(null);
    await refresh();
    props.onSelect(values.name);
  };

  const segments = loaded?.name.split('.') ?? [];

  return (
    <>
      <AppBar
        page={props.page}
        onNavigate={props.onNavigate}
        controls={
          <>
            {loaded && <span className="rule-version">v{loaded.version}</span>}
            <button
              type="button"
              className="btn"
              disabled={!loaded || saving}
              onClick={() => void save()}
            >
              Save{dependents.length > 0 ? ` (${dependents.length})` : ''}
            </button>
          </>
        }
      >
        <span className="breadcrumb-sep">/</span>
        <span className="breadcrumb-item">Propositions</span>
        {/* A dotted name is already a path, so it renders as the trail rather than needing one. */}
        {segments.map((segment, index) => (
          <span key={`${segment}-${index}`}>
            <span className="breadcrumb-sep">/</span>
            <span className={index === segments.length - 1 ? 'breadcrumb-current' : 'breadcrumb-item'}>
              {segment}
            </span>
          </span>
        ))}
      </AppBar>

      {failure !== null && (
        <div role="alert" className="conflict-banner">
          {failure}
          {loaded && (
            <button type="button" className="btn" onClick={() => props.onSelect(loaded.name)}>
              Reload latest
            </button>
          )}
        </div>
      )}

      <DependentsStrip dependents={dependents} />

      <div className="shell-body with-rail">
        <PropositionExplorer
          entries={entries}
          selected={props.selected}
          actions={{
            onSelect: (name) => props.onSelect(name),
            onDerive: (name) => {
              // Prefilled to the source's namespace, so a derivation lands beside its origin.
              const namespace = name.includes('.') ? `${name.slice(0, name.lastIndexOf('.'))}.` : '';
              const entry = entries.find((candidate) => candidate.name === name);
              setDialogError(null);
              setDialog({
                name: namespace,
                modelType: entry?.modelType ?? modelTypes[0] ?? 'customer',
                deriveFrom: name,
              });
            },
            onNew: () => {
              setDialogError(null);
              setDialog({ name: '', modelType: modelTypes[0] ?? 'customer', deriveFrom: null });
            },
            onDelete: (entry) => void remove(entry),
          }}
        />
        <EditorPane client={props.client} />
        <JsonPane />
        <EvaluatePane client={props.client} />
      </div>

      {dialog && (
        <PropositionDialog
          seed={dialog}
          modelTypes={modelTypes.length > 0 ? modelTypes : ['customer']}
          error={dialogError}
          onCancel={() => { setDialog(null); setDialogError(null); }}
          onCreate={(values) => void create(values)}
        />
      )}
    </>
  );
}
```

- [ ] **Step 6: Wire it into `App`**

In `ui/apps/demo/src/App.tsx`, replace the Task 18 placeholder so the `propositions` branch renders `PropositionsPage`, and add its import. The routing markup from Task 18 Step 6 is already written against the real props, so only the placeholder substitution and the import change.

- [ ] **Step 7: Style the dialog and strip**

Append to `ui/apps/demo/src/styles/app.css`:

```css
/* ---------------------------------------------------------------------------
   New/Derive dialog and the dependents strip.
   --------------------------------------------------------------------------- */
.dialog-backdrop {
  position: fixed;
  inset: 0;
  display: grid;
  place-items: center;
  background: rgb(0 0 0 / 45%);
  z-index: 10;
}

.dialog {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  width: min(28rem, calc(100vw - 2rem));
  padding: var(--space-4);
  border-radius: var(--radius-2);
  background: var(--sh-surface);
  box-shadow: 0 10px 30px rgb(0 0 0 / 35%);
}

.dialog-title {
  margin: 0;
  font-size: 1rem;
}

.dialog-field {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.dialog-field small {
  opacity: 0.7;
}

.dialog-error {
  margin: 0;
  color: var(--sh-danger, #b91c1c);
}

.dialog-actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-2);
}

/* Kept in view while editing rather than sprung at save time. */
.dependents-strip {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: var(--space-2);
  padding: var(--space-2) var(--space-3);
  border-bottom: 1px solid var(--sh-border);
  font-size: 0.85rem;
}

.dependents-strip ul {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  margin: 0;
  padding: 0;
  list-style: none;
}
```

Substitute real token names as in Task 19 Step 4 — check `tokens.css`.

- [ ] **Step 8: Run tests and typecheck**

```bash
cd ui && pnpm --filter @motiv/rules-demo test && pnpm --filter @motiv/rules-demo typecheck
```

Expected: PASS, fifteen tests.

If `seeds the dialog from the derived-from node` reports an empty value, the seed is being computed from `props.selected` rather than the node passed to `onDerive` — they agree here but will not once derivation is offered from a non-selected node.

- [ ] **Step 9: Commit**

```bash
git add ui/apps/demo/src ui/apps/demo/test
git commit -m "feat(demo): author, derive and cascade propositions from a dedicated page"
```

---

### Task 21: End-to-end proof, full verification, and simplification

The single test that proves the feature's central claim, then the checks CLAUDE.md requires before this can be called done.

**Files:**
- Create: `ui/apps/demo/e2e/propositions.spec.ts`
- Modify: `README.md`
- Create: `docs/propositions/index.md`, `docs/propositions/toc.yml`
- Modify: `docs/toc.yml`, `docs/Overview.md`

**Interfaces:**
- Consumes: everything.
- Produces: no new code interfaces.

- [ ] **Step 1: Write the failing e2e test**

Create `ui/apps/demo/e2e/propositions.spec.ts`. Read `ui/apps/demo/e2e/live-rules.spec.ts` first and reuse its host fixture and helpers verbatim — per commit 58fae21a the suite must never adopt a host it did not start, and that logic lives in the existing fixture.

```typescript
import { expect, test } from '@playwright/test';

test.describe('propositions', () => {
  test('an authored proposition becomes a building block a rule follows', async ({ page }) => {
    // The feature's central claim, end to end: author a proposition, reference it from a rule,
    // then edit the proposition and watch the rule's verdict change without the rule being touched.
    await page.goto('/#/propositions');

    // Author a proposition over a compiled spec.
    await page.getByRole('button', { name: /^new$/i }).click();
    await page.getByLabel(/name/i).fill('customer.e2e-eligible');
    await page.getByRole('button', { name: /create/i }).click();
    await expect(page.getByRole('treeitem', { name: /e2e-eligible/ })).toBeVisible();

    // Point it at "is active".
    await page.getByRole('treeitem', { name: /e2e-eligible/ }).click();
    // Use the DSL surface to set the body — reuse the helper from dsl.spec.ts for this.
    await page.getByRole('tab', { name: 'DSL' }).click();
    await page.getByRole('textbox').first().fill('customer.is-active');
    await page.getByRole('button', { name: /^save/i }).click();

    // Reference it from a live rule.
    await page.goto('/#/rules');
    await page.getByRole('button', { name: /rule/i }).click();
    await page.getByRole('option', { name: 'can-checkout' }).click();
    await page.getByRole('tab', { name: 'DSL' }).click();
    await page.getByRole('textbox').first().fill('customer.e2e-eligible');
    await page.getByRole('button', { name: /^save$/i }).click();

    // Evaluate an inactive adult: not eligible.
    await page.getByRole('button', { name: /evaluate/i }).click();
    await expect(page.getByTestId('evaluation-result')).toContainText(/false/i);

    // Redefine the proposition — the rule is never touched again.
    await page.goto('/#/propositions/customer.e2e-eligible');
    await page.getByRole('tab', { name: 'DSL' }).click();
    await page.getByRole('textbox').first().fill('customer.is-adult');
    await page.getByRole('button', { name: /^save/i }).click();

    // The rule's verdict follows.
    await page.goto('/#/rules');
    await page.getByRole('button', { name: /evaluate/i }).click();
    await expect(page.getByTestId('evaluation-result')).toContainText(/true/i);
  });

  test('the blast radius is shown before saving', async ({ page }) => {
    await page.goto('/#/propositions');
    await page.getByRole('button', { name: /^new$/i }).click();
    await page.getByLabel(/name/i).fill('customer.e2e-base');
    await page.getByRole('button', { name: /create/i }).click();

    await page.getByRole('button', { name: /derive/i }).click();
    await page.getByLabel(/name/i).fill('customer.e2e-derived');
    await page.getByRole('button', { name: /create/i }).click();

    await page.goto('/#/propositions/customer.e2e-base');

    await expect(page.getByText(/changing this affects 1 proposition/i)).toBeVisible();
    await expect(page.getByText('customer.e2e-derived')).toBeVisible();
  });

  test('a referenced proposition cannot be deleted', async ({ page }) => {
    await page.goto('/#/propositions/customer.e2e-base');

    await page.getByRole('button', { name: /^delete$/i }).click();

    await expect(page.getByRole('alert')).toContainText('customer.e2e-derived');
  });
});
```

**Adapt the selectors** to what the demo actually renders — `getByTestId('evaluation-result')` and the evaluate-button name are guesses. Read `EvaluatePane.tsx` and the existing e2e specs, and use their real selectors. Do not add test ids to production components if an accessible role already identifies the element.

- [ ] **Step 2: Run the e2e suite to verify it fails**

```bash
cd ui && pnpm --filter @motiv/rules-demo e2e -- propositions.spec.ts
```

Expected: FAIL on the first missing selector. Iterate on selectors — not on production code — until the flow drives the real UI.

- [ ] **Step 3: Make it pass**

Fix only selector mismatches and genuine bugs the test exposes. If a real bug surfaces, write the narrower unit test for it first in the appropriate Phase 1–5 test file, fix it, then return here.

- [ ] **Step 4: Run the e2e suite**

```bash
cd ui && pnpm --filter @motiv/rules-demo e2e
```

Expected: PASS, including every pre-existing spec.

- [ ] **Step 5: Run every suite**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test Motiv.slnx -f net10.0
```

```bash
cd ui && pnpm -r test && pnpm -r typecheck
```

Expected: PASS throughout. Per CLAUDE.md the Poker/ECommerce/SmartHome example tests assert on justification strings — fix any that the spec renaming in Task 14 shifted.

- [ ] **Step 6: Document the feature**

Per CLAUDE.md, user-facing documentation lives outside CLAUDE.md. Add:

- **`README.md`** — a brief example under Core Features:

```markdown
### Runtime propositions

Propositions are the building blocks rules are made of. Register them in C#, or author them at
runtime and persist them server-side — either way a document references them by name:

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddPropositions(new JsonFilePropositionStore("propositions.json"))
    .AddRule<CanCheckoutRule>();
```

Names are namespaced with dots (`customer.eligibility.is-active`), an authored document may override
a compiled spec, and editing a proposition rebinds every rule and proposition that references it —
transactionally, so an edit that would break a dependent is refused whole.
```

- **`docs/propositions/index.md`** — the detailed page, following the structure of a sibling
  feature directory under `docs/`. Cover: authoring, namespacing, overriding and reverting, the
  cascade and its all-or-nothing guarantee, quarantine on startup, the `IPropositionStore` seam,
  and the HTTP surface table from the spec.
- **`docs/propositions/toc.yml`**, plus entries in **`docs/toc.yml`** and **`docs/Overview.md`**.

Read an existing feature directory under `docs/` first and match its conventions rather than
inventing a layout.

- [ ] **Step 7: Simplify (mandatory)**

Per CLAUDE.md this step is not optional. Spawn a `code-simplifier` agent over the changed code:

```
Review the runtime-propositions implementation for duplication, convoluted design, procedural code,
long methods, and other anti-patterns. Scope: src/Motiv.Serialization/Propositions/, the
ISpecSource/LayeredSpecSource changes, src/Motiv.Serialization/Rules/, the AspNetCore proposition
endpoints and wiring, ui/packages/rules-core/src/namespaceTree.ts, and ui/apps/demo/src/explorer/ +
panes/PropositionsPage.tsx.

Constraints that must survive any refactor:
- The evaluation hot path stays free of added indirection and allocation (see commits 3e28b314,
  e1cbf47d, 87f9e65f, 7dc2ca39).
- DependencyGraph.DependentClosure must keep its topological ordering. The negative test
  `Should_never_place_a_dependent_before_something_it_depends_on` exists precisely to stop this
  being "simplified" into a plain reachability walk.
- Prepare-all-then-commit-all must stay two-phase; collapsing it loses the all-or-nothing guarantee.
- CLAUDE.md warns against over-DRYing: the duplication between binder families is deliberate.
```

Apply its improvements and re-run the affected tests.

- [ ] **Step 8: Re-run everything after simplification**

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
dotnet test Motiv.slnx -f net10.0
```

```bash
cd ui && pnpm -r test && pnpm -r typecheck && pnpm --filter @motiv/rules-demo e2e
```

Expected: PASS throughout. Only claim completion once every command above has actually been run and passed — evidence before assertions.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "test(demo): prove the proposition cascade end to end, and document the feature"
```

---

## Deferred to the parameters spec

Not in this plan, by design — see the spec's *Deferred: parameterised propositions*:

- Reference-site arguments: `{ "spec": "orders.at-least-n-large", "args": { "n": 5 } }`
- Per-callsite binding, with resolution returning a *document to bind on demand* rather than a bound spec
- An args-keyed bind cache, and its invalidation via the reverse-dependency index this plan builds
- Argument editors at reference sites in both the builder and the DSL
- Argument validation against the callee's declarations

Documents may continue to declare and use parameters exactly as they do today; what this plan adds
no support for is *supplying arguments at a reference*.
