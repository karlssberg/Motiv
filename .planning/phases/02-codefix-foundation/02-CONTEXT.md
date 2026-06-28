# Phase 2: CodeFix Foundation - Context

**Gathered:** 2026-02-07
**Status:** Ready for planning

<domain>
## Phase Boundary

CodeFix generates clean, context-aware proposition code with meaningful names. Derive class/model names from expression content instead of hard-coding "Proposition"/"Model". Remove Debug.WriteLine from generated output. This phase covers naming and cleanup only — new statement contexts (if/while/ternary) belong in later phases.

</domain>

<decisions>
## Implementation Decisions

### Name derivation strategy
- Names derive from **expression content**, not method name or variable context
- Find the **common root** identifier across the expression:
  - `age > 18` → root is `age`
  - `order.Total > 100 && order.IsActive` → common root is `order`
- Use **root only** for member access, not full path: `order.Total > 100` → `Order`, not `OrderTotal`
- Always **PascalCase** the derived name: `age` → `Age`, `isValid` → `IsValid`
- For `is` type-check expressions, derive from the **variable**, not the type: `obj is string` → `Obj`

### Proposition and Model naming
- Proposition and Model names differ only by suffix: `AgeProposition` and `AgeModel`
- Suffixes are **always** `Proposition` and `Model` — no context-aware suffix adaptation
- Fallback when no common root can be found (unrelated variables like `x > 5 && y < 10`): use generic `Proposition` and `Model`

### Name clash resolution
- If a generated name already exists in the compilation, append incrementing integers: `OrderProposition` → `OrderProposition1` → `OrderProposition2`
- Same logic applies to the Model name

### Debug.WriteLine removal
- Remove all Debug.WriteLine calls from generated code
- Remove `using System.Diagnostics` if no longer needed by other code in the file

### Claude's Discretion
- Exact algorithm for identifying the "common root" across complex expressions
- How to handle edge cases in PascalCase conversion (acronyms, numbers, etc.)
- Whether to preserve the original expression as a comment in generated code
- Import cleanup strategy details

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches for implementation. The naming heuristic should be practical and produce sensible names for typical C# boolean expressions.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 02-codefix-foundation*
*Context gathered: 2026-02-07*
