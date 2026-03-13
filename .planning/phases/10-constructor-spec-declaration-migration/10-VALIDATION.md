---
phase: 10
slug: constructor-spec-declaration-migration
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-13
---

# Phase 10 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (via Roslyn code fix testing framework) |
| **Config file** | none (auto-discovered) |
| **Quick run command** | `dotnet test src/Motiv.CodeFix.Tests/Motiv.CodeFix.Tests.csproj -q` |
| **Full suite command** | `dotnet test /c/Dev/Motiv/Motiv.sln -q` |
| **Estimated runtime** | ~60 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test src/Motiv.CodeFix.Tests/Motiv.CodeFix.Tests.csproj -q`
- **After every plan wave:** Run `dotnet test /c/Dev/Motiv/Motiv.sln -q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 10-01-01 | 01 | 1 | SFMK-01 | code inspection + integration | `dotnet test src/Motiv.CodeFix.Tests -q` | ✅ | ⬜ pending |
| 10-01-02 | 01 | 1 | SFMK-02 | code inspection + integration | `dotnet test src/Motiv.CodeFix.Tests -q` | ✅ | ⬜ pending |
| 10-01-03 | 01 | 1 | SFMK-03 | regression | `dotnet test src/Motiv.CodeFix.Tests -q` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. The constructor spec path is already exercised by 4+ tests in `MotivConvertToSpecTests` and `MotivConvertToSpecWithDebugOutputTests`.

---

## Manual-Only Verifications

All phase behaviors have automated verification.

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
