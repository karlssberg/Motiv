---
title: Motiv.Explain Diagnostics
description: The warnings Motiv.Explain's analyzer reports for [Explain] methods and uses that an explain build cannot explain.
---

The analyzer runs in every build, explain build or not, so apart from MOTIV1002 its warnings don't come and go
with the switch.

| ID | Reported on | Meaning |
|---|---|---|
| MOTIV1001 | A method group, such as `values.Count(IsBig)` | Calls through the delegate won't be explained. Call the method directly in a lambda (`v => IsBig(v)`) if you want them explained. |
| MOTIV1002 | A call to an `[Explain]` method declared in another project | Interceptors only reach calls in the project that declares the method. The other project needs its own `[Explain]` method. |
| MOTIV1003 | An `[Explain]` method | Its type, or a type around it, isn't `partial`. |
| MOTIV1004 | An `[Explain]` method | It can't be explained, and the message says why: not a single `bool` expression, generic, an extension method, a local function, overridable, uses `base`, has `ref`/`out`/`in` parameters or a ref struct or pointer parameter, is in a struct or interface, or has a signature the rest of the assembly can't see. |

MOTIV1002 needs `[Explain]` to be visible in the other project's metadata. Only explain builds keep it there,
so in other builds the warning doesn't appear, which is harmless because nothing is explained in them either.
