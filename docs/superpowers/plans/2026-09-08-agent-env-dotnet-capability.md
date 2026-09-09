# Agent environment — The capability a session was asserting rather than measuring — Plan

**Date:** 2026-09-08
**Ticket:** [#173](https://github.com/karlssberg/Motiv/issues/173)
**Source:** none. This is not a bundle-spec slice — #173 is not a child of the build map
[#169](https://github.com/karlssberg/Motiv/issues/169), and no §6 bullet on any of the four bundle
specs asks for it. Its specification is its own three options, and it was raised by
[#167](https://github.com/karlssberg/Motiv/pull/167) (spec 4J) as an environment constraint the
series had been working around.
**Lineage:** #167 (the disclosure written by hand) → [#174](https://github.com/karlssberg/Motiv/pull/174)
(option 3: the constraint written into `CLAUDE.md`) → this (options 1 and 2: the constraint
*measured*).

## Why this exists

Claude Code cloud containers for this repo carry no .NET SDK, and egress to the .NET distribution is
blocked, so one cannot be installed. `Motiv.Tests`, the `src/examples/*.Tests` suites, `pnpm e2e` and
`Motiv.Studio` are all unrunnable there; only the `ui/` workspace builds.

`CLAUDE.md` requires the full solution suite for any change to justification output, assertion text or
result formatting. In a container that instruction is unfollowable — and the failure is silent. The
session runs `pnpm -r test`, sees green, and reports it. Nothing in the environment says otherwise,
because *absence of a compiler produces no error until you invoke it*, and a session that has decided
its change is UI-only never does.

The ledger records the shape this took: #171 (spec 4K) sat unclaimed while cloud runs drained the
documentation backlog instead, and only shipped when a local session picked it up.

## What has already landed

**Option 3 is done.** PR #174 added `CLAUDE.md` §"Cloud containers cannot build the .NET side", which
states the constraint and the disclosure obligation. That is the cheap, honest half, and the ticket
says to do it regardless of whether 1 or 2 lands.

What remains is options 1 and 2 — provisioning — plus the sentence the ticket closes on, which is the
real design constraint:

> Option 3 is worth doing regardless of whether 1 or 2 lands, since a hook can fail and the agent
> should not infer capability from silence.

## The problem with what landed

`CLAUDE.md` today asserts a **fact**: *"There is no .NET SDK in a Claude Code cloud container for this
repo."* Three things are wrong with a fact in that position.

1. **It is not true everywhere it is read.** The same `CLAUDE.md` is loaded by local sessions, which
   have an SDK. A reader has to work out which situation it is in before the sentence means anything,
   and nothing tells it.
2. **It goes stale silently.** The moment a container gains an SDK — the whole point of options 1 and
   2 — a session reading that sentence declines work it could do, and has no way to notice.
3. **It cannot report a partial answer.** An SDK without the net8.0 and net9.0 runtimes builds the
   solution and fails two thirds of the test matrix. "There is / is not an SDK" has no way to say so.

So the deliverable is not *"install an SDK"*. It is *"replace an assertion with a measurement, and make
the measurement's answer the thing sessions read"*.

## Design

A `SessionStart` hook, `scripts/agent/dotnet-capability.sh`, registered in `.claude/settings.json`. It
emits one of three verdicts as session context:

| Verdict | Meaning | What the session must do |
|---|---|---|
| `PRESENT` | a usable SDK was found | full-solution rule applies as written |
| `PROVISIONED` | none found, one installed just now | as above; a `PARTIAL` line may narrow it |
| `UNAVAILABLE` | none found, none installable | disclose the unrunnable suites and the reason |

Ordering, and why each step is where it is:

1. **Resolve a muxer** — `PATH`, then `$DOTNET_ROOT/dotnet`, then the install root. A usable muxer is
   one whose `--list-sdks` reports a major version ≥ 8. The floor matters: an SDK 6 muxer is a `dotnet`
   on `PATH` that cannot restore this solution, and treating "a `dotnet` exists" as the test would
   report `PRESENT` for it.
2. **Replay a recorded failure** before probing again. A container that could not reach the
   distribution at 09:00 will not at 09:05, and a session-start hook that re-probes every session is
   a tax on every session.
3. **Fetch the install script, and treat that fetch as the egress probe.** One request, not two. In
   the blocked case — today's case — the whole hook costs `MOTIV_DOTNET_HOOK_TIMEOUT` seconds.
4. **Install the SDK channel, then the two runtime channels.** `dotnet test` runs the suite on
   net8.0, net9.0 and net10.0, so an SDK alone leaves two thirds of the matrix unrunnable. A runtime
   channel that fails downgrades the verdict's *detail* rather than passing unmentioned.
5. **Re-resolve the muxer afterwards.** `dotnet-install.sh` exits 0 for a channel with no build for
   the platform. Exit 0 is not evidence.

Constraints the script is written under:

- **Never fails a session.** Every path exits 0. A hook that can break session start is worse than no
  hook.
- **No runtime dependency beyond coreutils.** In particular no `python3` and no `jq`: a hook that
  needs a tool on `PATH` in order to report that another tool is missing from `PATH` is a hook that
  can fail exactly when it is needed. JSON escaping is done in bash.
- **No `timeout(1)`.** macOS does not have it, and a wrapper that silently does not exist runs the
  fetch unbounded. `curl --max-time` instead.

## Testing

TDD, with the environment fabricated rather than borrowed: `PATH` is replaced by a directory of stubs
(`dotnet`, `curl`, and the install script `curl` writes), `HOME` and the state and install roots are
redirected into a scratch tree. No case touches the real network or the real `~/.dotnet` — including
by accident, which is why `new_env` installs a failing `curl` by default rather than leaving the real
one reachable.

Cases: an SDK on `PATH` (and that it does *not* probe the network); an SDK below the floor; a muxer
only under `$HOME/.dotnet`; blocked egress; the recorded failure replayed, and re-probed under
`FORCE`; a full provision; a provision missing its runtimes; a failed SDK install; `SKIP`; and the
worst environment available, to confirm exit 0.

The verdict text is asserted by content, not shape — `UNAVAILABLE` must name every unrunnable suite
and restate the disclosure obligation, because that text *is* the deliverable.

## Expected fallout

The suite will pass before it deserves to. It asserts what the script says, and the script is the
thing being written, so a green suite proves agreement rather than correctness. **Every case will be
mutation-proved**, on the ledger's standing lesson that a gate is worth only what it refuses — and
this is a gate whose green everything downstream reads as evidence.

The mutation I expect to survive is the post-install re-resolution (step 5), because the natural stub
makes a successful install always produce a working muxer, so nothing distinguishes *verified* from
*assumed*.

## Not in scope

- **Option 2, widening the network policy.** It is not a repo change; nothing in this tree can grant
  egress. What this slice can do is make the day it changes *observable* — the recorded reason says
  `egress-blocked` with the URL and the timeout, so a later session's `FORCE` re-probe answers the
  question directly.
- **Verifying a successful provision against the real distribution.** It cannot be done from a local
  session, which resolves at step 1 and never reaches the installer. See the design doc's
  "What is not verified".
