---
title: Getting Started with Motiv
description: Learn how to get started with the Motiv library for building specifications and propositions
---

# Getting Started with Motiv

## Introduction

Motiv is a .NET library that allows you to create specifications and propositions for validating business rules in a fluent, readable way. This guide will help you get started with using Motiv in your projects.

## Installation

You can install Motiv via NuGet:

```bash
dotnet add package Motiv
```

Or via the NuGet Package Manager:

```
Install-Package Motiv
```

## Basic Concepts

Motiv is built around several core concepts:

- **Specifications**: Reusable, composable business rules
- **Propositions**: Logical statements that can be evaluated against models
- **Results**: Objects returned when propositions are evaluated, containing success/failure status and explanations

## Your First Specification

Here's a simple example to get you started:

```csharp
using Motiv;

// Define a model
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
}

// Create a specification
var isAdult = Spec
    .Build<Person>(person => person.Age >= 18)
    .WhenTrue("Person is an adult")
    .WhenFalse("Person is underage")
    .Create();

// Use the specification
var person = new Person { Name = "John", Age = 25 };
var result = isAdult.IsSatisfiedBy(person);

// Check the result
if (result.Satisfied)
{
    Console.WriteLine(result.Explanation); // "Person is an adult"
}
```

## Combining Specifications

Motiv allows you to combine specifications using logical operators:

```csharp
var isValidName = Spec
    .Build<Person>(person => !string.IsNullOrWhiteSpace(person.Name))
    .WhenTrue("Person has a valid name")
    .WhenFalse("Name cannot be empty")
    .Create();

var isValidPerson = isAdult.And(isValidName);

// Or you can use the operator overload:
// var isValidPerson = isAdult & isValidName;
```

## Next Steps

- Explore the [Builder](./builder/index.md) API for creating complex specifications
- Learn about [Operators](./operators/index.md) to combine specifications
- See how to work with [Collections](./collections/index.md) of specifications and results

For a complete API reference, see the [API Documentation](../api/Motiv.yml).
