---
phase: 02-codefix-foundation
plan: 01
subsystem: code-generation
tags: [roslyn, name-derivation, tdd, semantic-analysis]
requires:
  - phase-01 (analyzer infrastructure)
provides:
  - expression-name-derivation
  - collision-detection
affects:
  - 02-02 (debug cleanup - will use derived names)
  - 02-03 (integration - will use derived names)
tech-stack:
  added: []
  patterns:
    - frequency-based-common-root-extraction
    - semantic-model-collision-detection
    - is-pattern-variable-extraction
key-files:
  created:
    - src/Motiv.CodeFix/ExpressionNameDeriver.cs
    - src/Motiv.CodeFix.Tests/ExpressionNameDeriverTests.cs
  modified: []
decisions:
  - id: fallback-naming
    choice: Use "Proposition"/"Model" when no common root identifier exists
    rationale: Ensures algorithm always produces valid names for unrelated variable expressions
  - id: alphabetical-tie-breaking
    choice: Use alphabetical ordering for deterministic tie-breaking in frequency counts
    rationale: Ensures consistent, predictable behavior across runs
  - id: is-pattern-variable-only
    choice: For is-pattern expressions, extract only from variable side, not pattern type
    rationale: Derive from user's domain variable (obj) not C# type name (string)
metrics:
  duration: 3 min
  completed: 2026-02-08
---

# Phase 2 Plan 01: Expression Name Derivation Summary

**One-liner:** Frequency-based identifier extraction algorithm with semantic collision detection, producing PascalCased Proposition/Model names from boolean expression content.

## What Was Built

Created the algorithmic core of Phase 2: `ExpressionNameDeriver` class that analyzes boolean expression syntax trees to extract meaningful class names. The algorithm implements a multi-step pipeline: (1) extract root identifiers from expression syntax, (2) find common root via frequency counting, (3) convert to PascalCase, (4) append Proposition/Model suffixes, (5) detect collisions using semantic model, (6) resolve collisions with incrementing numbers.

**Key capabilities:**
- Single identifier extraction: `age > 18` → `AgeProposition`, `AgeModel`
- Member access root detection: `order.Total > 100` → `OrderProposition` (not `OrderTotalProposition`)
- Common root frequency analysis: `order.Total > 100 && order.IsActive` → `OrderProposition`
- Fallback for unrelated variables: `x > 5 && y < 10` → `Proposition`, `Model`
- Is-pattern variable extraction: `obj is string` → `ObjProposition` (from variable, not type)
- Semantic collision detection: `age > 18` with existing `AgeProposition` → `AgeProposition1`

## Task Commits

| Task | Type | Description | Commit | Files |
|------|------|-------------|--------|-------|
| 1 | test | Write failing tests for ExpressionNameDeriver | 5d9b192 | ExpressionNameDeriverTests.cs |
| 2 | feat | Implement expression name derivation algorithm | 2d858af | ExpressionNameDeriver.cs |

## Verification Results

All success criteria met:

- ✓ ExpressionNameDeriver exists with DeriveClassNames public method
- ✓ 8 unit tests cover all scenarios (single var, member access, common root, is-expression, fallback, PascalCase, collisions)
- ✓ All tests pass
- ✓ Algorithm matches LOCKED decisions from CONTEXT.md exactly
- ✓ `dotnet test src/Motiv.CodeFix.Tests --filter "ExpressionNameDeriver"` passes (8/8)
- ✓ `dotnet build src/Motiv.CodeFix` compiles without errors

## Decisions Made

### Fallback Naming Strategy
**Context:** Expressions with unrelated identifiers (e.g., `x > 5 && y < 10`) have no clear common root.

**Decision:** Return generic `"Proposition"` and `"Model"` as fallback when all identifiers are distinct and multiple exist.

**Rationale:** Ensures algorithm always produces valid names. Frequency count alone can't resolve cases where every identifier appears exactly once. Fallback provides sensible default without requiring hard-coded type name lists.

**Alternatives considered:**
- Pick first identifier alphabetically: Less intuitive, doesn't signal "no clear root"
- Throw exception: Would require caller to handle, complicates usage

**Impact:** CodeFix will generate generic names for complex multi-variable expressions without a dominant identifier pattern.

### Alphabetical Tie-Breaking
**Context:** Multiple identifiers may have equal frequency counts (e.g., `person.Age > 18 && company.Revenue > 1000` - both roots appear once).

**Decision:** Use `.ThenBy(g => g.Key)` for deterministic alphabetical ordering after frequency sorting.

**Rationale:** Ensures consistent behavior across runs. Without deterministic tie-breaking, order depends on internal collection ordering, which can vary.

**Alternatives considered:**
- No tie-breaking (use first): Non-deterministic, tests could be flaky
- Use position in source: Complex to implement, less predictable

**Impact:** Predictable, testable behavior for ambiguous cases.

### Is-Pattern Variable Extraction
**Context:** `IsPatternExpressionSyntax` has two sides: the tested expression (left) and the pattern/type (right).

**Decision:** Extract identifiers only from `isPattern.Expression`, not from `isPattern.Pattern`.

**Rationale:** Per LOCKED decision, derive from user's domain variable (`obj`) not C# type name (`string`). Type names are framework types, not domain concepts.

**Implementation:** Special-case `IsPatternExpressionSyntax` in `GetIdentifiersFromExpression()` to walk only the Expression subtree.

**Impact:** `obj is string` produces `ObjProposition`, not `StringProposition`.

## Technical Details

### Algorithm Complexity
- Root identifier extraction: O(n) where n = syntax nodes in expression
- Frequency counting: O(m log m) where m = unique identifier count
- Collision detection: O(c) where c = collisions (typically 0-2)

**Performance:** Optimized for typical expressions (1-5 variables). No caching needed - executes once per CodeFix invocation.

### Key Implementation Patterns

**Member Access Root Detection:**
```csharp
// Exclude right side of member access: order.Total -> keep "order", skip "Total"
identifier.Parent is MemberAccessExpressionSyntax memberAccess
    && memberAccess.Name == identifier
```

**Variable Symbol Filtering:**
```csharp
// Reuses existing pattern from ConvertToSpecCodeFix.cs (line 372-383)
symbol is IFieldSymbol or IPropertySymbol or ILocalSymbol or IParameterSymbol
```

**Collision Detection:**
```csharp
// Uses Roslyn semantic model for scope-aware lookup
semanticModel.LookupSymbols(position, name: name)
    .Any(s => s.Kind == SymbolKind.NamedType)
```

### Edge Cases Handled

1. **Empty expression (no identifiers):** Returns fallback `"Proposition"`
2. **Is-pattern with complex pattern:** Only extracts from tested expression
3. **Nested member access:** `person.Address.City` → extracts root `"person"`
4. **Name collision with multiple existing types:** Increments until unique (`AgeProposition`, `AgeProposition1`, `AgeProposition2`)
5. **Already PascalCase identifiers:** `Capitalize()` preserves existing uppercase

## Deviations from Plan

None - plan executed exactly as written.

## Next Phase Readiness

**Blockers:** None

**Concerns:** None

**Ready for 02-02 (Debug.WriteLine Removal):** Yes. The derived names are available for use in generated code. Next plan will integrate this into the CodeFix provider and remove Debug.WriteLine statements.

**Integration points:**
- `ConvertToSpecCodeFix.cs` will call `ExpressionNameDeriver.DeriveClassNames()` instead of hard-coding "Proposition"/"Model"
- Insertion position for collision detection comes from namespace declaration span

## Self-Check: PASSED

All claimed files exist:
```
FOUND: src/Motiv.CodeFix/ExpressionNameDeriver.cs
FOUND: src/Motiv.CodeFix.Tests/ExpressionNameDeriverTests.cs
```

All claimed commits exist:
```
FOUND: 5d9b192
FOUND: 2d858af
```
