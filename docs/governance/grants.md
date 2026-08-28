---
title: Namespace Grants
---

Namespace grants answer *what may this authenticated caller read and write*, layered on top of the
[secure-by-default](index.md#authenticate) endpoints. They are entirely opt-in: without a registered
`IGrantSource`, every check on this page passes automatically, so the surface behaves exactly as it did
before namespace grants existed — authenticated, unfiltered.

## `IGrantSource`

```csharp
public interface IGrantSource
{
    bool SupportsAdministration { get; }
    IReadOnlyCollection<string> KnownRoles { get; }
    IReadOnlyList<NamespaceGrant> GrantsFor(ClaimsPrincipal principal);
    bool IsAdministrator(ClaimsPrincipal principal);
}
```

Register an implementation as a singleton (`services.AddSingleton<IGrantSource>(...)`) to turn grant
checks on. `SupportsAdministration` is a hint for whether an admin surface should offer in-app grant
management; `KnownRoles` is the role universe the [lockout pre-check](approval-gate.md#lockout-pre-check)
consults; `GrantsFor` yields a principal's namespace grants; `IsAdministrator` gates reconfiguring the
gate itself (`PUT`/`DELETE /gate`), a separate, non-namespaced capability.

## `NamespaceGrant` and the verb ladder

```csharp
public sealed record NamespaceGrant(string Prefix, GrantVerb Verb);
public enum GrantVerb { Read, Author, Publish }
```

`GrantVerb` is a ladder — `Read < Author < Publish` — and enum order is load-bearing: a grant of a
higher verb covers every lower one on the same namespace. `NamespaceGrant("pricing.eu", GrantVerb.Publish)`
covers `Read`, `Author`, and `Publish` on anything under `pricing.eu`.

## Namespace covering

```csharp
public static bool Covers(string prefix, string name);
```

`NamespacePrefix.Covers` is dot-boundary matching, shared by grant evaluation and the
`change.in-namespace` [gate spec](approval-gate.md) so "covers" means one thing everywhere: the empty
prefix covers everything; otherwise `prefix` must equal `name` or end exactly on one of its dotted
segments — `"pricing"` covers `"pricing.eu.vat"` but not `"pricing-eu.vat"`.

## Unfiltered read, filtered write

Every listing endpoint (`GET /catalog`, `GET /rules`, `GET /rules/{name}`, `GET /propositions`,
`GET /propositions/{name}`, `GET /change-requests`, `GET /change-requests/{id}`, `GET /gate`) is
**unfiltered** — no grant check runs, so any authenticated caller sees the whole listing. Every write
checks a verb against the names it touches:

| Endpoint | Verb checked | Against |
|---|---|---|
| `POST /validate`, `POST /evaluate` | `Author` or `Publish`, on *any* namespace | — |
| `PUT`/`DELETE /rules/{name}` | `Publish` | the rule's name |
| `POST`/`PUT`/`DELETE /propositions[/{name}]` | `Publish` | the proposition's name |
| `POST /change-requests` | `Author` | every proposed target's name |
| `POST /change-requests/{id}/approvals`, `/rejection`, `/publish` | `Publish` | every target in the request |
| `POST /change-requests/{id}/withdrawal` | none — the caller must be the request's own author | — |
| `PUT`/`DELETE /gate` | `IsAdministrator` (not a namespace verb) | — |

A refusal is `403 { "error": "Requires the '<verb>' grant on '<name>'." }` (the author-anywhere and
administrator checks answer with their own fixed wording).

## Example

`src/Motiv.Studio/GrantSources.cs` has three real implementations worth reading
end to end: a zero-config `DevGrantSource` that grants a single dev principal everything, a mutable
`JsonFileGrantSource` with in-app administration and a last-administrator invariant, and a
`ClaimsGrantSource` that maps IdP role claims to namespace grants via configuration — the one exercised
by the [Keycloak walkthrough](index.md#trying-it-with-a-real-idp).

## Next Steps

- See [The Approval Gate](approval-gate.md) for the `administer` grant's other consumer: reconfiguring
  the gate.
- See [Change Requests](change-requests.md) for how `Author` and `Publish` map onto the governance
  workflow's own verbs (propose vs. land).
- See the [Governance overview](index.md) for how this fits between authentication and the approval gate.
