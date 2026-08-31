---
description: Claim and implement the next wayfinder bundle-spec slice
argument-hint: "[ticket number or slice, e.g. 171 or 4K]"
allowed-tools: Bash(git:*), Bash(which:*), Read, Edit, Write, Glob, Grep
---

Continue the wayfinder **build** phase for the enterprise-grade-product map.

## Environment

- `gh`: !`which gh >/dev/null 2>&1 && echo present || echo "ABSENT — use the GitHub MCP tools; mapping is in docs/agents/issue-tracker.md"`
- .NET SDK: !`which dotnet >/dev/null 2>&1 && dotnet --version || echo "ABSENT — the .NET suites, pnpm e2e and Motiv.Studio cannot run here (see issue #173). A slice touching C# is blocked; say so rather than reporting a partial run as green."`

Shipped slices, newest first — the commit titles, not the docs, are the accurate history:

!`git log origin/main --oneline -40 | grep -E "Spec [0-9]" | head -20`

## 1. Pick the slice

The ledger is the **build map, issue #169** (`wayfinder:map`). The discovery map #100 is closed history — do not read it for progress.

If `$1` is given, that is the slice; read its ticket and skip to step 2.

Otherwise run the frontier query from `docs/agents/issue-tracker.md`: list #169's open children, drop any with an assignee or an unclosed issue named in a `Blocked by:` line, and take the first in map order. State which ticket you picked and why before doing anything else.

If the frontier is empty, or every candidate is blocked or assigned, **stop and report that** — do not invent a slice, and do not fall back to guessing the next letter from filenames.

## 2. Read the source spec

Each ticket names its bundle spec. They live on a branch, not on `main`:

```
git fetch origin wayfinder/enterprise-grade-product
git show origin/wayfinder/enterprise-grade-product:.scratch/enterprise-grade-product/specs/<n>-<name>.md
```

Read the whole spec, not just the §6 bullet — §5 invariants and §7 verification obligations are what the slice is judged against. Follow its citations back to the decision tickets on #100 where a choice looks arbitrary; those decisions are locked and are not yours to reopen.

## 3. Claim it

Assign the ticket to the driving dev before the first write — the frontier query treats an assignee as claimed, so an unassigned ticket invites a second session onto the same work.

## 4. Build it

Follow `CLAUDE.md`: TDD strictly (write the failing test, watch it fail for the right reason, then implement), and the mandatory `code-simplifier` pass afterwards.

Scope is one shippable PR. A slice is not a spec bullet — spec 3's three §6 steps produced five slices. If the bullet is bigger than one PR, cut it and open a follow-up ticket on #169 rather than widening.

## 5. Definition of done

A slice is not finished until all of these are true:

- Tests pass, and you say plainly which suites you could **not** run and why.
- **The plan is written to `docs/superpowers/plans/YYYY-MM-DD-spec-<slice>-<name>.md` and the design to `docs/superpowers/specs/YYYY-MM-DD-spec-<slice>-<name>-design.md`, in the same commit as the implementation.** Ten of the first nineteen slices skipped this and the ledger lost track of the series; that is the specific failure this step exists to prevent. Cite the source bundle spec at the top, as the 3A–3E designs do.
- The #169 ledger row is added, with the PR number and the docs column ticked.
- The ticket is closed with a comment saying what landed.
