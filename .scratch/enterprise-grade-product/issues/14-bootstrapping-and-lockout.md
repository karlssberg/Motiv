# Bootstrapping and lockout for a self-governing gate

Type: grilling
Status: resolved
Blocked by: 13

## Question

This ticket exists *because* of the decision to express approval as a Motiv rule. A gate that governs
changes can govern changes to itself, and that is a genuine failure mode — not a hypothetical one.

**How does the system avoid becoming ungovernable?**

The session must resolve:

1. **Who approves a change to the approval rule?** If the approval rule governs itself, an admin can
   publish a rule requiring approval from a role nobody holds — and no one can approve the fix. The
   candidate answers are all unattractive in different ways: exempt the approval rule from itself
   (a hole in the model), require a distinct super-role for it (a second mechanism), or make it
   file/config-backed and not editable through the UI at all (loses the "configure it with the same
   editor" appeal that motivated the decision).
2. **Is a lockout even detectable in advance?** A rule is a predicate over a `ChangeRequest`. Before
   publishing a *new* approval rule, the system could evaluate it against a synthetic change request
   representing "an attempt to change this rule back" and refuse if nothing could ever satisfy it.
   That is a genuinely interesting use of the engine — establish whether it is sound, and whether
   it is decidable given parameterised and async specs.
3. **Cold start.** A fresh deployment has no users, no grants, and no approval rule. What is the
   first administrator, and where does that identity come from? An environment-variable bootstrap
   admin, an IdP claim, or a first-run setup flow.
4. **The break-glass path.** When the gate is wrong at 3am, what is the documented escape? A
   configuration flag that disables the gate and *loudly* audits that it was disabled is the usual
   answer. Confirm the shape, and confirm it is auditable rather than silent.
5. **Async specs in the gate.** If an approval rule references an async spec and the dependency is
   down, publishing blocks. Should the gate be restricted to synchronous specs, and is that
   restriction enforceable at bind time?

The last two are the ones most likely to be discovered in production rather than in design, which is
why this is a ticket and not fog.

## Answer

**Every lockout surface is recovered from the layer beneath it, bottoming out at infrastructure access.
The self-governance paradox dissolves once gate *reconfiguration* is an `administer` action (the
authorization layer) rather than a `may-publish` action (the workflow layer) — so the gate never governs
itself. A sound-but-incomplete pre-check stops the common footgun; a config-layer break-glass is the
floor; the gate is restricted to synchronous specs.**

### The unifying principle — layered recovery

| Lockout surface | Escaped by | Layer |
|---|---|---|
| Bad **rule/proposition** | `publish` grant | workflow |
| Bad **gate** | `administer` verb | authorization |
| No **admins** left / grant lockout | bootstrap seed, then break-glass | config / infra |

Each layer's lockout is recoverable from the one below; the chain bottoms out at **infrastructure
access** (env vars / redeploy). "Ungovernable" is impossible while someone holds infra access — the
correct floor, since no software design can help once *that* is lost. The rest of this ticket is the
application of that principle.

### Sub-1 — the gate never governs itself; gate changes are `administer`-gated

The ticket's three options (exempt / super-role / config-only) are all unattractive because they try to
solve self-reference *within the workflow layer*. It dissolves instead: **reconfiguring the active gate
is an `administer`-verb action (ticket 12), not a `may-publish` action.** The gate governs *content*
(rules, propositions); *who may change the gate* is an authorization question ticket 12 already modeled.
No self-reference to patch, and:
- **No new mechanism** — `administer` already exists (it is present whenever the grant source is
  mutable, per 12).
- **No model hole** — it is a principled *layering* (workflow control over content vs access control
  over configuration), the same line 12 drew between the gate and grants.
- **The "same editor" appeal survives** (option c's loss does not apply) — the gate is still authored as
  a Motiv `may-publish` rule with the built-in specs; only *permission to publish a gate change* is
  `administer`.

### Sub-2 — a sound-but-incomplete lockout pre-check

Before publishing a candidate gate, evaluate it against a **synthetic, maximally-approvable gate-change**
`ChangeRequest` (max approvals; approvers holding every known role; not self-approved; targeting the
gate's namespace). If even *that* is blocked, no real change could pass → **refuse the publish, with
`Justification` naming why.**

- **Soundness/decidability:** general predicate satisfiability is **undecidable** (specs carry arbitrary
  C#/expression-trees; async specs hit external state), so a gate cannot be *proven* safe. But evaluating
  one synthetic change always terminates, so the check soundly catches gates that reject *everything* —
  the common footgun (a role nobody holds, an impossible approver count). It is a **footgun-catcher, not
  a proof**, and honest about being incomplete.
- The "every known role" universe comes from the grant source / IdP-claims config (12); a gate
  referencing a role outside that universe is itself the lockout signal.
- A nice dogfooding property: the engine detects its own potential lockout by evaluating itself. Async
  specs make it strictly more incomplete → sub-5.

### Sub-5 — the gate is restricted to synchronous specs, enforced at bind time

An async spec in the gate couples **authoring availability** to an external dependency (every publish
waits on it) and defeats the sub-2 pre-check (a gate that calls out cannot be reasoned about). So the
**binder rejects any gate document that composes an async spec**, with a clear error. Enforceable
because async is a first-class property the binder already tracks (`IsAsync` on `RuleSetEntry`).
Distinct from ticket 13's `change.touches-async-spec`, which is a **synchronous** predicate asking
whether the *governed change* touches an async spec — the gate may *ask* that without *being* async.

### Sub-3 — cold start: a config-designated identity, elevated only while no admin exists

- **Dev** is already solved — ticket 12's dev single-user source *is* the first admin (zero-config).
- **Production** (empty app-owned store): a **configured bootstrap identity** — a subject or IdP
  claim/group named in config (env var / appsettings) — is treated as holding `administer` **only while
  the grant store contains no `administer` grant.** A *conditional seed*, not a standing superuser: once
  a real admin exists, the elevation goes inert, so a leaked bootstrap config does nothing thereafter.
  This unifies the env-var and IdP-claim answers as one mechanism.
- **Reject the open first-run setup flow** — an unauthenticated setup endpoint before auth is configured
  is the same default-credentials hazard ticket 08 exists to prevent.

### Sub-4 — break-glass: a deploy-time flag, gate off, loud and audited

The documented 3am escape: a **configuration flag (env var / appsettings — requires redeploy/ops
access, never an in-app toggle)** that disables the gate (`may-publish` always true) while active.
- **Loud and audited**, never silent: continuous warning while active (like ticket 08's dev identity),
  and **every publish made under break-glass is stamped in the audit trail** (ticket 10) as such.
- It lives at the **infra layer** — a higher privilege than any in-app grant — which is precisely what
  makes an app-level lockout always escapable by ops.
- **Recommended hardening (flag):** time-box it so a forgotten break-glass auto-expires rather than
  silently staying open.

## Downstream

- **Closes the governance spine** (10 → 11 → 13 → 14). No ticket is blocked on 14.
- **To ticket 12:** relies on `administer` being the authority for gate-config changes and on a
  "cannot remove the last `administer`" invariant in the app-owned grant store (the grant-lockout twin
  of gate-lockout) — worth noting there as an implementation invariant.
- **To ticket 15 (audit/decision log):** break-glass publishes and bootstrap elevations are
  audit-trail events with distinguishing markers.
- **New machinery this ticket implies:** the synthetic maximally-approvable `ChangeRequest` builder for
  the sub-2 pre-check, and the bind-time "gate must be synchronous" check.
