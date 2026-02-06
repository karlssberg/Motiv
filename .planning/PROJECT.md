# Motiv

## What This Is

A .NET library that solves "Boolean Blindness" by transforming boolean expressions into explainable propositions (Specification Pattern). Developers write boolean logic and get back not just true/false, but structured explanations of *why* — enabling decision transparency, debugging, and human-readable output. Ships with a Roslyn analyzer and code fix that detect boolean expressions and offer to convert them into Motiv specs.

## Core Value

Boolean expressions must produce meaningful, structured explanations — not just true/false. If users can't understand *why* a result occurred, the library has failed.

## Current Milestone: v1.0-rc1 Polish

**Goal:** Polish expression tree support, analyzer, and codefix to release-candidate quality.

**Target features:**
- Expand analyzer to detect `is`/pattern-matching expressions
- Improve codefix: derive names from context, remove Debug.WriteLine, handle if/while/ternary contexts
- Ternary codefix: map true/false branches to WhenTrue/WhenFalse fluent methods
- Increase test coverage for analyzer and codefix edge cases
- Review and improve XML doc quality on existing public APIs

## Requirements

### Validated

<!-- Shipped and confirmed working. -->

- ✓ Expression tree support — full parity with BooleanPredicate path
- ✓ Analyzer (MOTIV0001) — detects comparison and logical operators in boolean expressions
- ✓ CodeFix — converts boolean expressions to Motiv specs (return, local decl, assignment contexts)
- ✓ Multi-target — .NET 8, 9, 10, .NET Standard 2.0
- ✓ Analyzer and CodeFix bundled in NuGet package

### Active

<!-- Current scope. Building toward these for v1.0-rc1. -->

- [ ] Analyzer detects `is` type-check and pattern-matching expressions
- [ ] CodeFix derives names from context instead of hard-coding "Proposition"/"Model"
- [ ] CodeFix does not inject Debug.WriteLine
- [ ] CodeFix handles if, while, do-while conditions
- [ ] CodeFix handles ternary expressions with WhenTrue/WhenFalse integration
- [ ] Edge-case test coverage for analyzer and codefix
- [ ] XML doc quality review on core + expression tree public APIs

### Out of Scope

<!-- Explicit boundaries. -->

- Full IDE integration beyond Roslyn analyzer/codefix — too broad for RC
- Performance optimization — no evidence of issues
- Additional NuGet package metadata/branding — cosmetic, post-RC

## Context

- Branch `feature/expression-trees-support` is 200+ commits ahead of main
- Expression tree support is functionally complete with full test coverage
- No TODOs, FIXMEs, NotImplementedExceptions, or skipped tests in codebase
- Analyzer currently detects: comparison operators (==, !=, <, >, <=, >=) and logical operators (&&, ||, !)
- CodeFix currently hard-codes names "Proposition" and "Model", always injects Debug.WriteLine
- CodeFix only handles return statements, local declarations, and assignments
- Examples exist: ECommerce, Poker, SmartHome

## Constraints

- **Target frameworks**: net8.0, net9.0, net10.0, netstandard2.0 — must maintain all
- **Analyzer/CodeFix**: Must target netstandard2.0 (Roslyn requirement)
- **Backward compatibility**: Existing Motiv API surface must not break
- **Testing framework**: xUnit with AutoFixture/NSubstitute per CLAUDE.md

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Derive codefix names from context | Hard-coded names are a poor DX; context-derived names are more useful | — Pending |
| Remove Debug.WriteLine from codefix | Generated code should be clean; debugging is user's choice | — Pending |
| Ternary branches → WhenTrue/WhenFalse | Natural mapping; ternary already expresses true/false outcomes | — Pending |
| Analyzer: `is`/pattern only (not method calls) | Focused expansion; method calls deferred to avoid false positives | — Pending |
| XML docs: review quality, not coverage | Coverage already 100%; focus on accuracy and helpfulness | — Pending |

---
*Last updated: 2026-02-06 after requirements definition*
