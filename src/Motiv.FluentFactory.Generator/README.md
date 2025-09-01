# Motiv.FluentFactory.Generator

A source generator that automatically creates fluent API builders from your existing constructors, providing type-safe
object creation with minimal runtime overhead.

## Overview

The Motiv.FluentFactory.Generator transforms ordinary constructors into powerful fluent factory APIs. By analyzing
constructor parameters at compile time, it generates type-safe fluent interfaces that guide developers through object
creation using a structured, step-by-step approach.

```

[FluentFactory]
public partial class Shape;

[FluentConstructor(typeof(Shape), CreateMethodName = "CreateRectangle")]
public record Rectangle(int Width, int Height);

[FluentConstructor(typeof(Shape), CreateMethodName = "CreateDiamond")]
public record Diamond(int Width, int Height);

[FluentConstructor(typeof(Shape), CreateMethodName = "CreateSquare")]
public record Square(int Width);


// Fluent API usage:                               
var rectangle = Shape.WithWidth(5).WithHeight(10).CreateRectangle();                   
var diamond   = Shape.WithWidth(5).WithHeight(10).CreateDiamond();
var square    = Shape.WithWidth(5).CreateSquare();      
```

### Key Benefits

- **Type-Safe Construction**: The generated API leverages the type system to ensure all required parameters are provided
  before object creation
- **No Configuration**: Works with any class or struct without requiring base classes, interfaces, or structural
  modifications
- **IntelliSense-Guided**: Constructors are selected at design time, leveraging IntelliSense and the type
  system to guide developers through decision-tree paths that match constructor signatures
- **Near-Zero Runtime Overhead**: All generation occurs at compile time, allowing the JIT compiler to optimize the
  fluent API calls away, leaving only the direct constructor invocation

### How It Works

The generator analyzes your constructors and creates fluent APIs with the following characteristics:

1. Each constructor parameter becomes a method in the fluent chain
2. Parameter order determines the sequence of fluent method calls
3. Object instantiation is only permitted when all required parameters are satisfied
4. Multiple constructors generate branching paths in the decision tree

## Attributes

The generator provides several attributes to control fluent factory creation and customize the generated API.

### FluentFactoryAttribute

Mark classes or structs for fluent factory generation. This attribute signals the generator to create a fluent API for
the annotated type.

```csharp
[FluentFactory]
public parital class MyClass
{
    // Your class implementation
}
```

### FluentConstructorAttribute

Mark specific constructors for inclusion in fluent factory generation. Use this attribute when you want to selectively
include only certain constructors in the generated fluent API.

### FluentMethodAttribute

Customize parameter mapping in the fluent chain by specifying method names and execution priority. This attribute allows
fine-grained control over the generated fluent interface.

```csharp
public MyClass([FluentMethod("WithName")] string name,
               [FluentMethod("WithAge", Priority = 1)] int age)
{
    // Constructor implementation
}
```

### FluentMethodTemplateAttribute

Define template-based method generation for creating multiple fluent methods from a single pattern. This attribute
enables the generator to create fluent methods based on predefined templates, providing a scalable approach for handling
multiple similar operations.

```csharp
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public class FluentLogLevelMethods
{
    [FluentMethodTemplate]
    public static LogLevel AsDebug() => LogLevel.Debug;

    [FluentMethodTemplate]
    public static LogLevel AsInfo() => LogLevel.Info;

    [FluentMethodTemplate]
    public static LogLevel AsWarning() => LogLevel.Warning;

    [FluentMethodTemplate]
    public static LogLevel AsError() => LogLevel.Error;
}
```

### MultipleFluentMethodsAttribute

Generate multiple fluent methods for a single parameter based on a variants type. The `VariantsType` property specifies
an enum or static class containing the possible values, creating distinct fluent methods for each variant.

```csharp
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

[FluentFactory]
public static partial class LogEntryFactory;

public class LogEntry
{
    [FluentConstructor(typeof(LogEntryFactory))]
    public LogEntry(
        [MultipleFluentMethods(typeof(FluentLogLevelMethods))] LogLevel level,
        string message)
    {
        Level = level;
        Message = message;
    }

    public LogLevel Level { get; }
    public string Message { get; }
}

// The generated fluent API creates separate methods for each LogLevel value:
// var debugEntry = LogEntryFactory.AsDebug().WithMessage("Debug information").Create();
// var infoEntry = LogEntryFactory.AsInfo().WithMessage("General information").Create();
// var warningEntry = LogEntryFactory.AsWarning().WithMessage("Warning message").Create();
// var errorEntry = LogEntryFactory.AsError().WithMessage("Error occurred").Create();
```

## FluentOptions

Configure generation behavior using the `FluentOptions` enumeration:

- **`None`**: Default behavior with no special options applied
- **`NoCreateMethod`**: Omits the `Create()` method from generation, causing immediate constructor invocation when all
  parameters are resolved

## Getting Started

1. **Install** this package in your project
2. **Annotate** your classes and constructors with the appropriate attributes
3. **Build** your project to generate the fluent factory classes
4. **Use** the generated fluent APIs in your code

## Requirements

- .NET Standard 2.0 or higher
- C# 7.0 or later for optimal experience

## License

This package is part of the Motiv project. Please refer to the main project repository for licensing information.
