---
title: Capture Postures
description: DecisionCaptureRegistry and the three input-capture postures — StoreWhole, Redact, and ReferenceOnly — and the bind-time refusal that makes choosing one mandatory rather than advisory.
---

Replay is impossible without the evaluated input. Storing the input means storing whatever your model
holds — names, ages, financial facts. Motiv cannot make that trade on your behalf, so it is a seam, and
a seam with **no default**.

## The Three Postures

```csharp
var options = new DecisionLogOptions();
options.Capture
    .ReferenceOnly<Customer>(customer => customer.CustomerId)   // recommended for production
    .Redact<Order>(order => new { order.Total })                // you decide what survives
    .StoreWhole<CartLine>();                                    // development only
```

| Posture | Records | Replay | Privacy |
|---|---|---|---|
| `StoreWhole<T>()` | the model as evaluated | complete | raw PII — **development only** |
| `Redact<T>(projection)` | the projection's output | as far as the mask left it | you choose |
| `ReferenceOnly<T>(keySelector)` | a key, and nothing else | via your system of record | **GDPR-tractable** |

Postures are keyed by **model type** — which is where a typed projection has to live, and which matches
a posture being a deployment-wide decision rather than a per-rule one. Registering a second posture for
the same type replaces the first, so tightening one does not require unregistering the old.

**The posture you choose is the replay ceiling.** It is the explicit trade of privacy against replay
fidelity, made once per deployment.

## Why `ReferenceOnly` Is Recommended

It makes erasure and audit *coexist*. Erase the subject in your own system of record, and:

- the decision record survives, carrying no personal data;
- the record still proves what was decided, when, under which rule version;
- replay correctly becomes impossible, because the input it needed is genuinely gone.

That is the right post-erasure state, not a compromise.

## The Bind-Time Refusal

A rule marked `audited` over a model type with no registered posture **does not bind**:

```
$.audited: rule 'can-checkout' is marked audited, but no capture posture is registered for
'Customer'; choose one with DecisionLogOptions.Capture — ReferenceOnly is recommended for production
```

The code is `RuleErrorCode.AuditCaptureNotConfigured`. A rule set built with no `DecisionLog` at all
gets the same refusal with a different reason.

Putting the check at bind time puts the refusal in three places at once:

- a **governed publish** is rejected, with the reason on the change request;
- a **startup load** reports it in the `RuleLoadReport`;
- a **replica** deployed without the posture *quarantines* the rule and says why — rather than silently
  logging whatever the model happens to hold, or silently ignoring the flag.

All three are the fail-closed behaviour, not an oversight. A whole-model default that applied by
omission would be the default-credentials trap wearing a compliance badge.
