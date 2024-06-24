# Introduction

This tutorial will guide you through the essential features of using Motiv, and how to use it.
It briefly explains the basic concepts of propositions, assertions, and specifications, and how they are used in 
Motiv, and then goes on to show how to create and compose propositions and specifications.

# Definitions

## Propositions

In logic, a proposition is a declarative statement that can be either *true* or *false*. For example:

* *Password must contain at least one number*
* *Data processing is complete*
* *User access is restricted to read-only*
* *The product is in stock*
* *Payment is successful*

Propositions can be combined to form more complex expressions, such as:
> _the product is in stock_ **&** _the payment is successful_

It's important to note that propositions are unproven statements and need to be *evaluated* before they can assert
facts.

## Propositional Statement

In traditional propositional logic, _propositions_ are presented as human-readable textual statements.
However, for our purposes, we want to model propositions as objects.
We therefore explicitly introduce the concept of a _propositional statement_ to describe the textual form.

## Specifications

Specifications (i.e., _Specs_) are the building blocks of propositions in Motiv.
They are nodes in the logical syntax tree that together form propositions.
In some instances they are equal, such as when creating
[atomic propositions](https://en.wikipedia.org/wiki/Atomic_sentence).

## Assertions

Assertions are _statements of fact_ about a model.
They are contained in the results returned when a proposition is evaluated against a model.

* the product is in stock
* the payment is unsuccessful

When satisfied, an assertion statement often looks the same as its propositional statement.
The main difference is that an assertion is a *statement of fact*, while a proposition is a *statement of possibility*.

## Metadata

In the context of Motiv, metadata is additional information attached to a proposition.
It is typically a POCO (Plain Old CLR Object) used to provide more context about the proposition.
When the metadata is a `string`, it is processed as an *assertion*.

## Reason

The *reason* is a human-readable explanation of why a proposition is satisfied or not.
Unlike Assertions, _reasons_ are a serialized `string` explaining the causes.
If a proposition generates multiple assertions, the proposition will be described using its (single) _propositional
statement_.
If propositions have been composed using logic operators, then the *reason* will serialize the entire expression, so
preserve the relationship between clauses.