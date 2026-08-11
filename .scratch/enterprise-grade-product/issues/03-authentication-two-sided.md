# Authentication — what does the SDK owe, and what does the app wire?

Type: grilling
Status: resolved
Blocked by: —

## Question

There is **no authentication on any endpoint**. `app.MapMotivRules("/api/rules")` mounts `/catalog`,
`/validate`, `/evaluate`, rules CRUD, and propositions CRUD wide open, and the sample's
`/api/checkout` is equally open.

Deployment is self-hosted against the customer's own IdP, so the SDK must not depend on a specific
provider. **What is the SDK's authentication contract?**

Sub-questions:

1. **Does `MapMotivRules` become secure-by-default?** Today it maps anonymous endpoints. Making it
   `.RequireAuthorization()` by default is a breaking change for every existing adopter — including
   the demo, which is the zero-config evaluation surface. What is the opt-out, and is it loud enough
   that nobody ships open by accident? (Consider: fail-closed with an explicit
   `AllowAnonymous()` escape versus fail-open with a startup warning.)
2. **What identity abstraction does the SDK need?** Not `ClaimsPrincipal` directly — the RBAC model
   (ticket 12) is namespace-prefix-based and needs *subject*, *roles*, and eventually *tenant*. Is
   that an `IRuleUserAccessor` resolved at the edge, echoing the tenancy-seam decision? Note that
   this same accessor is the natural home for the tenancy seam: one ambient-scope abstraction rather
   than two.
3. **Machine vs human.** `/evaluate` and the app's own execution path are called by services, not
   people; authoring endpoints are called by humans through a browser. Do they share an auth model,
   or does the SDK distinguish an *execution* surface from an *authoring* surface? This split may be
   more load-bearing than the RBAC model itself — it decides whether an outage in the IdP stops rule
   *evaluation* or merely rule *editing*.
4. **What does the app demonstrate?** OIDC against which reference provider, and does the app ship a
   dev-mode identity so `docker compose up` still works without an IdP?

Blocks: 12 (RBAC grant model).

## Inherited from ticket 08

Sub-question 4 is answered: **the app ships a dev-mode identity** so `docker compose up` still yields
a working app with no IdP. Under evolve-in-place there is no separate demo — the product *is* the
evaluation surface — so this is load-bearing rather than a convenience.

**It must be fail-closed and loud**: refuse to start unless explicitly enabled, warn continuously
while active, never the default in a release-tagged image. A dev identity that can be switched on by
omission is a default-credentials vulnerability. How that is enforced belongs to this ticket.

## Answer

**The whole HTTP surface is authoring, so it is all authenticated. Secure by default with an explicit
opt-out; `ClaimsPrincipal` at the edge; OIDC demonstrated against Keycloak in an opt-in compose
profile and covered by e2e.**

### 0. The premise this ticket was written on was wrong

Sub-question 3 assumed a machine-facing execution surface that must stay anonymous. **There is none.**
No endpoint evaluates a *named live rule*. Execution happens **in-process**: the sample's
`/api/checkout` takes `CanCheckoutRule` and `FraudScreeningRule` by DI and calls `.Evaluate(customer)`
directly, never touching `MapMotivRules`.

So all thirteen endpoints are authoring endpoints, and the surface is uniformly protectable.

**This corrects a note pushed onto ticket 19.** That ticket records that authentication cannot
mitigate the stack-overflow class because `/evaluate` is *"the surface machine callers use — one of
the endpoints most likely to stay anonymous by design."* Wrong premise. With the whole surface
authenticated, authentication genuinely does reduce that attack surface to authenticated users.

### 1. `POST /evaluate` is an authoring tool

It exists so the Evaluate pane can test a **draft** — it takes an arbitrary `{modelType, document,
model}`, not a rule name. Authenticated with everything else. Production evaluation stays in-process.

It remains dual-use *by accident* — nothing stops an adopter pointing a service at it — so this is a
decision to not support that, not an observation that it is impossible.

### 2. Secure by default, with a visible opt-out

`MapMotivRules` applies `.RequireAuthorization()` to the group. Opening it requires an explicit,
greppable act at the call site (e.g. `MapMotivRules(path, o => o.AllowAnonymous())`) so an open
deployment is auditable in review rather than the silent default.

**Breaking for every existing adopter.** Taken now because the library is pre-1.0 and ticket 06 has
not yet committed to a compatibility policy — the same argument ticket 09 used for the async write
path.

**This only works because of ticket 08.** `.RequireAuthorization()` with no policy demands an
authenticated user, so on a host with no authentication configured every request 401s — including
`docker compose up`. Ticket 08's **fail-closed dev-mode identity** supplies the principal, so the
zero-config evaluation surface survives *and* the endpoints are genuinely protected. Neither decision
is comfortable alone; together they cost nothing.

### 3. Identity — `ClaimsPrincipal` at the edge, grant evaluator in the SDK

Authorization lives in `Motiv.Serialization.AspNetCore` using ASP.NET's own primitives — policies and
`ClaimsPrincipal`. The Motiv-specific part is a **namespace-grant evaluator** taking a principal,
which ticket 12 designs. No new identity abstraction for adopters to implement.

*Correction:* this ticket claimed the SDK could not use `ClaimsPrincipal`. It could —
`System.Security.Claims` is **base class library**, not ASP.NET. The reason to keep authorization in
the hosting package is that its machinery already lives there, not that the type is unavailable.

**The tenancy seam is explicitly *not* this abstraction.** Sub-question 2 proposed one accessor for
both; that conflates two axes. **Tenancy selects which `BindingScope` you get; identity authorizes
what you may do within it.** A background job with no user still has a tenant; an admin spanning
tenants has one identity across several scopes. They also sit at different layers — tenancy must
reach `BindingScope` in `Motiv.Serialization`, authorization belongs at the HTTP edge. Two seams.
→ corrects the fog patch "where the tenancy seam sits relative to `BindingScope`".

### 4. What the app demonstrates

Provider-agnostic OIDC configuration with nothing baked in, plus `docker compose --profile auth up`
bringing up **Keycloak**, and **e2e coverage of the authenticated path**. Default compose stays
zero-config on the dev identity.

The testing requirement is the load-bearing part. This session found two bugs and both lived in
untested seams — the DI wiring that made `/evaluate` blind to authored propositions, and `TestApp`
calling the explicit overload so 72 endpoint tests exercised a wiring no application uses. A
documented-but-unexercised OIDC path would be the third of the same kind, and its failure mode is
*the product is open*.

### Inherited obligation from ticket 08

The dev-mode identity must be **fail-closed and loud**: refuse to start unless explicitly enabled,
warn continuously while active, never the default in a release-tagged image. A dev identity that can
be switched on by omission is a default-credentials vulnerability. Enforcing that belongs to this
decision's implementation.

## Corrected by ticket 06

Section 2 calls secure-by-default "breaking for every existing adopter". **There are none** —
`Motiv.Serialization.AspNetCore` has never been published to NuGet. The demo is its only consumer,
and ticket 08 makes that the product. The change is free.
