# Does `RuleSet` get a persistence seam?

Type: grilling
Status: resolved
Blocked by: —

## Question

Propositions have a pluggable store; rules do not.

```csharp
public interface IPropositionStore          // public, swappable
{
    IReadOnlyList<StoredProposition> Load();
    void Save(StoredProposition proposition);
    void Delete(string name);
}
```

`RuleSet` is a `sealed class` holding `private readonly Dictionary<string, RuleBase> _rules`. Rules
are bound at startup from compiled defaults, mutated in-process by `Update`/`Revert`, and lost on
restart. The sample host works around this by reverting to the compiled default before and after
each e2e run — the test suite is *designed around* the absence of durability.

**Does `RuleSet` grow an `IRuleStore` mirroring `IPropositionStore`, and what exactly does it
persist?**

Sub-questions the session must resolve:

1. **Document or binding?** A store that persists `RuleDocument` JSON must re-bind (and can fail) at
   load. A store that persists bound rules cannot exist — `RuleBase` holds delegates. So load is
   necessarily a re-validation step, which means **startup can now fail on data**, not just on code.
   Is that acceptable, and what happens to a rule whose stored document no longer binds because a
   spec was removed from the registry?
2. **Where does the compiled default go?** `AddRule<CanCheckoutRule>()` binds a default immediately —
   fail-fast at startup. With a store, the default becomes a *fallback* for an absent row. Is a
   compiled default still authoritative, or does a stored document always win?
3. **Does this belong in the SDK at all?** The two-sidedness rule says abstraction in the SDK,
   implementation in the app. But `IPropositionStore` sets the precedent that the abstraction is
   `Motiv.Serialization`'s, not `Motiv.Serialization.AspNetCore`'s. Same layer for rules?
4. **Symmetry or unification?** Two stores, or one `IRuleAuthoringStore` covering both? They share
   `BindingScope`, and republishing a proposition already rebinds every rule referencing it — the
   all-or-none guarantee argues they are one transactional unit, not two.

This is the root of the Durability bundle: 09, 10, 15, and 16 all hang off it.

## Answer

**Yes — `RuleSet` gets an `IRuleStore`, in `Motiv.Serialization`, mirroring `IPropositionStore`.**

### 1. Layer and shape

`IRuleStore` lives beside `IPropositionStore` in `Motiv.Serialization`, same narrow
`Load` / `Save` / `Delete` shape, same write-ahead ordering discipline (§5). The hosting package was
rejected: an adopter embedding the SDK without ASP.NET Core still needs durable rules, and splitting
the stores across assemblies puts the rule store on the far side of the `BindingScope` both transact
under.

Rejected outright: **persisting nothing and having the app replay documents through `Update()` at
startup.** `Update` always increments the version and cannot be told to restore one, so versions
could not survive a restart and optimistic concurrency would be corrupted across every deployment.

### 2. The record — head row, `(Name, Version, DocumentJson?)`

`ModelType`, `MetadataType`, `IsAsync`, `IsPolicy`, and `Description` are all `{ get; }` fixed at
construction — they come from the C# class, not from data. The *entire* mutable state of a rule is
those three fields. `StoredRule` is therefore smaller than `StoredProposition`, which needs
`ModelType` precisely because an authored proposition has no declaring class.

**`Version` must be persisted in its own right — it is not derivable from the document.** `Revert`
moves the version forward while setting the document back to null:

| state | Version | DocumentJson |
|---|---|---|
| never edited | 1 | `null` |
| reverted after three edits | 5 | `null` |

A document-only store cannot tell these apart and would silently reset every reverted rule to v1,
breaking optimistic concurrency across a restart. So `DocumentJson` is **nullable in the record**,
meaning "on the compiled default, at this version".

One head row per rule — not an append-only log. History is ticket 10's concern and can be added
beside this rather than reshaping it. A deliberate bet that 10 will not demand append-only; if it
does, the schema and this interface are both revisited.

### 3. A stored document that no longer binds → **quarantine, with a fail-fast policy on top**

Mechanism mirrors `PropositionSet`, which already has this fully developed — *"quarantine exists so a
bad row costs its own row"*, and *"an unusable model type is a quarantine reason, never a reason to
throw and fail startup"*. A quarantined rule stays listed and repairable and refuses to evaluate.

**Falling back to the compiled default was rejected**, and the reason is the governance destination:
with an approval gate (ticket 13) in front of every change, a silent fallback means the rule reverts
to behaviour nobody approved, after a restart, with no error — the approval trail says one thing and
production does another. Availability-over-correctness is indefensible for the product whose promise
is explaining why.

*Policy* sits above the mechanism: the host decides whether **any** quarantined rule should fail
startup, honouring `Add`'s fail-fast contract without letting one bad row unconditionally take down
the app. Mechanism in the SDK, policy in the app. → constrains ticket 16.

### 4. Precedence, and the hole it leaves

A stored document always beats the compiled default; an absent row means code-defined.

**A rule on its compiled default changes behaviour on redeploy with no version bump.** The obvious
fix — fingerprint the default — only half works: `RuleDefault.Document(json)` hashes fine, but
`RuleDefault.Compiled(object spec)` is a `SpecBase` built from C# delegates with nothing stable to
hash. That is a property of allowing rules to be defined in code, not a gap to engineer around.

Resolution: accept that a code-defined rule tracks code, and have the **decision log record the
build/assembly identity alongside the rule version**, so a past decision stays explicable. The UI
already surfaces `code-defined default` as a distinct state. → constrains ticket 15.

### 5. Two stores, not one — the transactional coupling does not exist

The charting note claiming rules and propositions are "one transactional unit" was **wrong**.
`PrepareRebind` binds a rule's *current* document against a prospective source — a rebind does not
change the document, so it never writes a rule row. Proposition publishes write only the proposition
store; `Update`/`Revert` write only the rule store. **The two are never written in the same
transaction.**

So: two symmetrical stores with different records. Not unified (the union would always be
half-empty), and not a shared generic base — the repo's own guidance warns against over-DRYing
exactly this near-duplication, and the records differ because rules are code with a mutable document
slot while propositions are data all the way down.

### Ordering discipline inherited from `PropositionSet`

Everything fallible runs before anything that mutates: bind prospectively → check dependents →
**persist** → mutate memory → commit. The store is last-of-the-fallible, first-of-the-committing, so
a store failure leaves nothing live behind it and "all of it, or none" holds without explicit
rollback. → ticket 09 decides whether this survives a store that is slow and async.
