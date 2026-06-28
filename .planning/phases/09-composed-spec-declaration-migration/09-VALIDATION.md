---
phase: 9
slug: composed-spec-declaration-migration
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-12
---

# Phase 9 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (via Roslyn code fix testing framework) |
| **Config file** | none (auto-discovered) |
| **Quick run command** | `dotnet test src/Motiv.CodeFix.Tests/Motiv.CodeFix.Tests.csproj -q` |
| **Full suite command** | `dotnet test /c/Dev/Motiv/Motiv.sln -q` |
| **Estimated runtime** | ~30 seconds (CodeFix tests), ~120 seconds (full suite) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test src/Motiv.CodeFix.Tests/Motiv.CodeFix.Tests.csproj -q`
- **After every plan wave:** Run `dotnet test /c/Dev/Motiv/Motiv.sln -q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 09-01-01 | 01 | 1 | SFMC-01 | unit/integration | `dotnet test src/Motiv.CodeFix.Tests -q` | ✅ | ⬜ pending |
| 09-01-02 | 01 | 1 | SFMC-02 | integration | `dotnet test src/Motiv.CodeFix.Tests -q` | ✅ | ⬜ pending |
| 09-01-03 | 01 | 1 | SFMC-03 | integration | `dotnet test src/Motiv.CodeFix.Tests -q` | ✅ | ⬜ pending |
| 09-01-04 | 01 | 1 | SFMC-04 | integration | `dotnet test src/Motiv.CodeFix.Tests -q` | ✅ | ⬜ pending |
| 09-01-05 | 01 | 1 | SFMC-05 | regression | `dotnet test src/Motiv.CodeFix.Tests -q` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. The 94 existing tests in `Motiv.CodeFix.Tests` are the verification gate — no new test stubs needed.

---

## Manual-Only Verifications

All phase behaviors have automated verification.

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
