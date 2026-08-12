---
title: Break-Glass
---

Break-glass is the floor beneath the [approval gate](approval-gate.md): a deploy-time flag that
disables the gate entirely while active, for the moment a gate document is misconfigured badly enough
that even the [lockout pre-check](approval-gate.md#lockout-pre-check) did not catch it, or an
emergency change simply cannot wait for the ordinary ceremony.

## `BreakGlass`

```csharp
public sealed record BreakGlass(bool Enabled, DateTimeOffset? ExpiresUtc)
{
    public static readonly BreakGlass Off = new(false, null);
    public bool Active(DateTimeOffset nowUtc);
}
```

`AddGovernance()` registers `BreakGlass.Off` via `TryAddSingleton` — off is the default everywhere, so
enabling governance changes no publish behavior on its own. It is deliberately **not** an in-app
toggle: no endpoint or grant flips it. A host wanting break-glass registers its own instance *after*
`AddGovernance()` (`AddSingleton` overrides `TryAdd`):

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddGovernance();

builder.Services.AddSingleton(new BreakGlass(
    Enabled: builder.Configuration.GetValue<bool>("Motiv:BreakGlass:Enabled"),
    ExpiresUtc: builder.Configuration.GetValue<DateTimeOffset?>("Motiv:BreakGlass:ExpiresUtc")));
```

Reading `Enabled`/`ExpiresUtc` from configuration (an environment variable or `appsettings` entry) is
what makes this an infrastructure-layer privilege above any in-app grant, including `administer` —
flipping it requires ops access to the deployment, not a click inside the product. `ExpiresUtc` is a
deliberate omission, not a recommendation: set it so a break-glass window auto-expires rather than
being left open indefinitely.

## What it bypasses

Every write that would otherwise pass through the gate consults `BreakGlass.Active(DateTimeOffset.UtcNow)`
first:

- `POST /change-requests/{id}/publish` — the gate is not evaluated at all; the request's
  `PublishedUnderBreakGlass` flag is stamped `true`, visible in its response and every later
  `GET /change-requests/{id}`.
- Direct writes (`PUT`/`DELETE /rules/{name}`, `POST`/`PUT`/`DELETE /propositions[/{name}]`) — the
  same bypass, but reported only through the audit log below, since a direct write mints no
  `ChangeRequest` for the flag to live on.

A bypass is recorded only when the underlying write **genuinely succeeds** — a stale `baseVersion` or
an invalid document still fails on its own terms, break-glass or not, so a failed write is never logged
as an audited publish that never happened.

## Audit trail

Every bypassed publish is logged as a `LogWarning` under the fixed `"Motiv.Governance.Audit"` category,
so an operator can filter for it independently of which route emitted it:

```text
MOTIV-AUDIT break-glass publish: change request {ChangeRequestId} by {Author} published with the
approval gate DISABLED.

MOTIV-AUDIT break-glass publish: direct write of {Kind} '{Name}' by {Author} published with the
approval gate DISABLED.
```

For a direct write, this log line is the *only* record that break-glass was used — a governed change
request also carries `PublishedUnderBreakGlass` durably in its own state.

## Next Steps

- See [The Approval Gate](approval-gate.md) for the ceremony break-glass bypasses.
- See the [Governance overview](index.md) for where break-glass sits among the other recovery layers.
