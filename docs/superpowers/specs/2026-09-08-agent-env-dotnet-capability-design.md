# Agent environment — The capability a session was asserting rather than measuring — Design

**Date:** 2026-09-08
**Ticket:** [#173](https://github.com/karlssberg/Motiv/issues/173)
**Plan:** [`2026-09-08-agent-env-dotnet-capability.md`](../plans/2026-09-08-agent-env-dotnet-capability.md)
**Source:** none — see the plan. #173 is an environment ticket, not a bundle-spec slice, and is not a
child of the build map [#169](https://github.com/karlssberg/Motiv/issues/169).
**Lineage:** [#167](https://github.com/karlssberg/Motiv/pull/167) (spec 4J, which raised it) →
[#174](https://github.com/karlssberg/Motiv/pull/174) (option 3) → this (options 1 and 2).

## What shipped

- `scripts/agent/dotnet-capability.sh` — a `SessionStart` hook that measures whether the session can
  build and test the .NET side, provisions an SDK when it can, and reports `PRESENT`, `PROVISIONED`
  or `UNAVAILABLE` as session context.
- `.claude/settings.json` — registers it, with `timeout: 900`.
- `scripts/agent/tests/dotnet-capability.test.sh` — 40 assertions over 13 fabricated environments.
- `.github/workflows/agent-env.yml` — shellcheck, the suite, and a check that `settings.json` still
  registers the hook.
- `Makefile` — `hooks-lint` and `hooks-test` targets, which the workflow invokes rather than
  restating.
- `CLAUDE.md` — §"Cloud containers cannot build the .NET side" becomes §"Whether this session can
  build the .NET side is measured, not assumed".

## The defect was in a sentence, not in code

#173's three options read as a menu — provision the SDK, widen the network policy, or write the
constraint down. Option 3 landed first because it was cheapest, and it works: PR #167's disclosure was
written by hand, and #174 made it a rule.

But the rule it made is a **fact**, and a fact is the wrong shape for this. `CLAUDE.md` said *"There is
no .NET SDK in a Claude Code cloud container for this repo"* — a claim that

- is false in a local session reading the same file,
- becomes false in a container the moment option 1 or 2 lands, with nothing to notice it, and
- cannot express the state where an SDK exists but two of its three test runtimes do not.

So the shipped change is not really "install an SDK". It is **replace an assertion with a
measurement, and make the measurement the thing sessions read.** `CLAUDE.md` now tells a session to
read the verdict and what each verdict obliges, rather than telling it what the verdict will be. The
statement of *today's* answer survives, dated, as an explanation of why the probe exists — not as the
thing to act on.

This generalises past this ticket, and is the environment-level form of a lesson the ledger already
carries in two places: **a claim about a check is not checked by that check passing** (4H), and **state
the limit where the green is read** (#208). Here the claim was about the *machine*, and the place it
was read was every session start.

## The one that was genuinely hard: exit 0 is not evidence

The suite went green before it deserved to. It asserts what the script says, and the script was being
written at the same time, so agreement is all a first green proves. Every case was therefore mutation-
proved. Two rounds:

| Mutation | Caught? |
|---|---|
| M1 drop the runtime-channel installs | ✅ 4 failed |
| M2 never record the failure | ❌ → bad mutation (see below); ✅ once corrected |
| M3 suppress the partial-install caveat | ✅ 2 failed |
| M4 accept an SDK below the floor | ✅ 1 failed |
| M5 `PROVISIONED` omits the install root | ❌ → bad mutation; ✅ once corrected |
| M6 `PRESENT` names neither version nor path | ✅ 2 failed |
| M7 conflate blocked egress with a failed install | ✅ 1 failed |
| **M8 do not re-verify the SDK after installing** | **❌ real hole** |
| M9 `UNAVAILABLE` drops the disclosure obligation | ✅ 1 failed |
| **M10 replayed verdict loses its reason** | **❌ real hole** |

**M8 was the find.** `dotnet-install.sh` exits 0 for a channel that resolves to no build for the
platform, so the hook re-resolves the muxer afterwards and downgrades to `UNAVAILABLE` if nothing
usable appeared. Nothing tested that. The reason is instructive: the stub installer created a working
muxer whenever it exited 0, so *success* and *claimed success* were the same event in every case, and
the re-resolution could be deleted with the suite green — leaving the hook to report `PROVISIONED`
with an empty version string. The fix was to teach the stub the shape that actually occurs (`exit 0`,
install nothing) and add the case for it. This is the same failure as a gate deriving its population
from a method name (#208): the test could not tell *verified* from *assumed* because its fixture never
separated them.

**M10 was smaller and worth keeping.** The replayed verdict could lose the *reason* it had recorded
and stay green. The reason is the entire value of recording it — `egress-blocked: <url> unreachable
within 8s` is what tells a later session whether the network policy has since changed, and a replay
saying only "because last time" answers nothing.

**Two of the first-round survivors were bad mutations, not surviving mutants.** M2 replaced the record
write's command with `:` and left the `>` redirect in place, so the file was still created; M5 deleted
the install root from one line while the next line still carried the muxer path, so the property under
test — *the path is handed over* — genuinely still held. Both were re-run corrected and both were
caught. **A surviving mutant is a hypothesis about the tests, not a verdict on them**, and checking
which of the two it is costs one re-run.

The round also found a defect in the *harness*: one case fell through to the real `curl` and reached
the internet, which is both slow and a lie about what it proved. `new_env` now installs a failing
`curl` by default, so reaching the network requires opting in.

**The battery was then re-run after the `code-simplifier` pass, and it had to be.** That pass replaced
`resolve_muxer`'s tab-packed `"<path>\t<version>"` return with two globals, which moves where a stale
value could be read from — the mutants were written against the old shape and stop being evidence
about the new one. Eleven mutants (the ten above with M2 and M5 corrected, plus one for the
`FULL_SUITE` sentence the pass extracted), all killed. **A refactor does not inherit the mutation
evidence of the code it replaced.**

## The gate's first run failed, and the reason was the gate

The workflow's first run went red on `SC2015` at `resolve_muxer`'s
`[ -n "$candidate" ] && [ -x "$candidate" ] || continue` — a line the author's local shellcheck
**0.11.0 did not report at all**. The runner image carried a different version.

Both halves were fixed, and the second is the one worth recording.

The line itself was genuinely the pattern shellcheck names, and the `-n` guard was redundant besides:
`[ -x "" ]` is already false, so it covers both the empty string `command -v` yields and the
`"/dotnet"` that `"${DOTNET_ROOT:-}/dotnet"` degenerates to when unset. It is now a single test.

But **the gate's verdict depended on which shellcheck the machine happened to have**, which makes
`make hooks-lint` passing locally not a statement about CI, and CI passing not a statement about a
contributor's box. Two answers is not one answer. The workflow now installs a pinned release rather
than apt's, named once in an `env:` block so the pin and the local expectation are visible in the same
place. This is the same family as 4I's import gate written weaker than the test it mirrored: the check
was reporting a property — *this shell is clean* — that it was not actually checking, because *clean*
was not a fixed thing.

Recorded here rather than swallowed, because it is the second gate in this slice to be wrong in a way
its own green could not show, after M8.

### And the third: the suite fabricated PATH but left the host's underneath it

The next run went red differently, and worse: **12 passed, 24 failed**. Every case from the third
onward. The stub `PATH` was `"$SANDBOX/bin:/usr/bin:/bin:…"` — the stubs first, then the real
coreutils the script needs — and **GitHub's ubuntu runners ship a real `dotnet` on `/usr/bin`**. So
`command -v dotnet` found a genuine SDK 10, every case resolved `PRESENT` at step 1, and the stub
`curl` was never invoked at all.

The same suite is green on a Mac, where the muxer lives at `/usr/local/share/dotnet` and nothing on
the fabricated `PATH` reaches it. **The fabrication was only ever partial, and which half was missing
was invisible on the machine it was written on.** That is the counterpart to the shellcheck pin one
paragraph up: there the gate's *tool* varied by machine, here its *fixture* did.

The fix is not to test for a host `dotnet` and skip. `new_env` now puts a working SDK-10 muxer on
`PATH` behind the stubs, in a `hostbin` dir, and shadows it with a `dotnet` that reports no SDKs — so
**every case runs under the hazard continuously** rather than under a `PATH` that happened to be clean.
One case asserts it directly, and deleting the shadow reproduces CI's failure locally to the
assertion: 12 passed, 26 failed (the two extra being that case's own).

### And the fourth, from review: the "never fails" case could not see the way it fails

Copilot's review found that `set -u` plus a bare `$HOME` in the *defaults* for `STATE_DIR` and
`INSTALL_ROOT` aborts the script on `HOME: unbound variable` — before it emits anything, breaking the
one guarantee it makes, and doing so in the environments least likely to have an SDK. Reproduced
directly: exit 1, no output.

The instructive part is **why the suite's own "the hook never fails the session" case could not see
it.** That case set `MOTIV_DOTNET_HOOK_STATE_DIR` to an unwritable path and deleted the state dir —
and `$HOME` is only expanded to compute the *default* of `STATE_DIR`, so overriding it is precisely
what hides the expansion. The case was built by making the environment hostile along the axes the
author was thinking about, and the crash was on the one axis that override removed. A test named for
a total property (*never fails*) tests exactly the paths it enumerates.

Fixed with a `HOME_DIR=${HOME:-${TMPDIR:-/tmp}}` fallback: without a HOME there is no durable
per-user location anyway, and a marker that does not outlive the container is still better than no
verdict at all.

Four gates, four ways of being green while wrong, and no two alike: M8 could not tell *verified* from
*assumed*; the lint could not tell *clean* from *clean-per-this-shellcheck*; the suite could not tell
*the hook found nothing* from *the host had something*; and the never-fails case could not tell
*survives a hostile environment* from *survives the three hostilities I listed*. None is visible from
the green — and the last one arrived from a reviewer after the other three had already been found by
adversarial passes, which is its own argument against treating a self-run mutation round as
exhaustive.

CodeQL separately flagged the workflow for carrying no `permissions` block. Fixed as `contents: read`,
matching `ui.yml`; the job reads a checkout and runs two Makefile targets, and publishes nothing.

## Decisions worth recording

**The fetch is the probe.** Rather than a `HEAD` request followed by a download, the hook downloads
`dotnet-install.sh` and treats failure as the egress verdict. One request; and in the blocked case —
today's — the whole hook costs one `--max-time`.

**`--max-time`, not `timeout(1)`.** macOS has no `timeout`, and a wrapper that silently does not exist
runs the fetch unbounded. (This repo has been bitten by exactly that before.)

**No `python3`, no `jq` at runtime.** A hook that needs a tool on `PATH` in order to report that
another tool is missing from `PATH` fails precisely when it is needed. JSON escaping is done in bash.
The *tests* use `python3` freely — they run where a failure is visible.

**`timeout: 900` in `settings.json`, and the ordering that makes it affordable.** A real SDK install
plus two runtimes exceeds the default hook timeout, and a hook killed mid-install leaves a broken
tree. Nine hundred seconds is only reachable by a container that can actually reach the distribution,
once, because the outcome is recorded — and the blocked path is gated *before* it, so today's
containers spend eight seconds, not fifteen minutes.

**The verdict always names the muxer path, even when `PATH` resolved it.** A hook's environment does
not outlive the hook, so `PATH` cannot be handed over. A muxer under `$HOME/.dotnet` is invisible to a
plain `dotnet`, and exporting `DOTNET_ROOT` does not make it visible — the path itself has to be used.
Without that line a session would meet `dotnet: command not found` and conclude the verdict had lied.

**A partial provision is a detail on `PROVISIONED`, not a fourth verdict.** `dotnet test` runs on
net8.0, net9.0 and net10.0. An SDK without the 8 and 9 runtimes builds everything and runs a third of
it — so the verdict carries a `PARTIAL` line naming the failed channels and the runnable
`--framework` subset. Adding a fourth verdict would have put the burden on every reader of the
verdict; putting it in the text puts it on the one reader who has it.

**`scripts/agent/`, not `.claude/hooks/`.** CI runs these files, and a workflow reaching into
`.claude/` to find its inputs reads as an accident. `.claude/settings.json` points out at them.

**The workflow checks that `settings.json` still registers the hook.** A malformed or renamed
registration disables the hook silently — which is the exact failure mode the hook exists to prevent,
reappearing one level up. The check is three lines and refuses a real thing.

**CI invokes `make hooks-lint` / `make hooks-test` rather than restating them.** The workflow
originally carried its own copy of the shellcheck glob, which is the smallest possible version of a
gate drifting from the thing developers run. Two names, one definition each; `Makefile` is in the
workflow's `paths` filter for the same reason.

## What is not verified

Stated here rather than glossed, because the green below is narrower than it looks.

- **The provisioning path has never run against the real .NET distribution.** It cannot be exercised
  from a local session, which resolves at step 1 and never reaches the installer; and it cannot be
  exercised from a cloud container, which is where egress is blocked — the condition the path exists
  to escape. What is proved is the hook's *behaviour given* an installer that succeeds, that fails,
  and that exits 0 having done nothing. What is not proved is that `dotnet-install.sh` invoked with
  these arguments installs a working SDK. Those arguments mirror `dotnet.yml`'s `setup-dotnet`
  versions (`8.0.x`, `9.0.x`, `10.0.x`), which is evidence about the *versions*, not about the
  installer invocation.
- **`UNAVAILABLE` has not been observed in a real cloud container**, only in a fabricated one. The
  first container session after this merges is the real first run.
- **Option 2 is untouched.** Nothing in this tree can grant egress. What this slice adds is that the
  day it changes becomes observable: the recorded reason names the URL and the timeout, and
  `MOTIV_DOTNET_HOOK_FORCE=1` re-probes on demand.

Suites run for this change: `make hooks-test` (40/40), `make hooks-lint` (clean), the workflow's
`settings.json` registration check, and the hook against this machine's real environment (`PRESENT —
SDK 10.0.203 at /usr/local/share/dotnet/dotnet`). The .NET suites were **not** run and did not need
to be — no C# is touched, and nothing here can affect a build. That disclosure is, fittingly, the
thing this slice is about.

## Where a later change will touch this

- **If the network policy is widened** (option 2), delete nothing here: run a container session,
  confirm the verdict flips to `PROVISIONED`, and update `CLAUDE.md`'s dated paragraph. The probe
  needs no change — that is the point of it being a probe.
- **If the solution's target frameworks move**, `SDK_CHANNEL` and `RUNTIME_CHANNELS` at the top of the
  script must move with them, and so must `dotnet.yml`. They are two copies of one fact; nothing
  currently ties them together, and that is a real gap rather than an oversight — closing it means
  parsing `Directory.Build.props` from bash, which costs more than it saves at two call sites.
