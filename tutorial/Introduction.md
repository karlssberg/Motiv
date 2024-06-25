# Introduction to Motiv

This tutorial will guide you through the essential features of Motiv and how to use it effectively.
We'll explore the basic concepts of propositions, assertions, and specifications, and then demonstrate how to create and
compose these elements within Motiv.

## Key Concepts

### Propositions

In logic, a proposition is a declarative statement that can be either *true* or *false*.
For example:

* "Password must contain at least one number"
* "Data processing is complete"
* "User access is restricted to read-only"
* "The product is in stock"
* "Payment is successful"

Propositions can be combined to form more complex expressions, such as:

> _The product is in stock_ **&** _The payment is successful_

It's important to note that propositions are unproven statements and need to be *evaluated* before they can assert
facts.
It may be helpful to think of them as terse _questions_ rather than _statements_.

#### Propositional Statements

While traditional propositional logic presents propositions as human-readable textual statements, Motiv models
propositions as objects.
Because of this, we introduce the concept of a _propositional statement_ to describe the textual form of a proposition.

### Specifications (Specs)

Specifications, or Specs, are the building blocks of propositions in Motiv.
They are nodes in the logical syntax tree that together form propositions.
In some cases, such as when creating atomic propositions, specs and propositions are equivalent.

### Assertions

`Assertions` are discrete _statements of fact_ about a model.
Proposition can yield one or more assertions regarding their possible outcomes, and when a proposition is evaluated, it
aggregates relevant underlying assertions to form a full explanation of the result.

Examples of assertions include:

* "Canned tomatoes in stock"
* "Payment is successful"

When satisfied, an assertion statement can sometimes be an exact copy of the propositional statement.
The key difference is that an assertion is a *statement of fact*, while a proposition is a *statement of possibility*.
Because of this, propositional statements are optional if they can be obtained from the assertion.

### Metadata

In Motiv, the term _metadata_ describes POCO objects that are used in substitution of assertions.
They provide rich contexts, principally for supporting multilingual explanations, that behave exactly same as
assertions.
However, their utility goes far beyond providing human-readable explanations, and can be instead used to conditionally
compose dynamic states and/or behaviours.

### Reason

A `Reason` is a human-readable `string` explaining why a proposition is satisfied or not.
Depending on how the proposition is composed, a reason may be a single assertion or a composition of multiple
assertions, or derived from the propositional statement.

## Next Steps

Now that we've covered the fundamental concepts, the following sections will demonstrate how to create and compose
propositions and specifications in Motiv.
We'll explore practical examples and best practices to help you leverage Motiv's capabilities effectively.
