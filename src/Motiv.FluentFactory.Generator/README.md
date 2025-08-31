# Motiv.FluentFactory.Generator

This package provides source generation and attributes for creating fluent API builders automatically.

## Overview

The Motiv.FluentFactory.Generator package contains both the attributes that can be applied to classes, constructors, and parameters, and the source generator that creates fluent builder patterns automatically. This enables the creation of readable, chainable APIs without the need to manually write builder classes.

## Attributes

### FluentFactoryAttribute

Applied to classes or structs to indicate that a fluent factory should be generated for the type.

```csharp
[FluentFactory]
public class MyClass
{
    // Your class implementation
}
```

### FluentConstructorAttribute

Applied to constructors to mark them for fluent factory generation.

### FluentMethodAttribute

Applied to parameters to define the method name and priority in the fluent chain.

```csharp
public MyClass([FluentMethod("WithName")] string name,
               [FluentMethod("WithAge", Priority = 1)] int age)
{
    // Constructor implementation
}
```

### FluentMethodTemplateAttribute

Applied to methods to define template-based method generation. This attribute allows the generator to create fluent methods based on method templates.

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

Applied to parameters that should generate multiple fluent methods based on a variants type. The VariantsType specifies an enum or static class containing the possible values.

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
        // Constructor implementation
    }
}

// Generated fluent API creates separate methods for each LogLevel:
// var logger = LogEntryFactory.AsDebug().WithMessage("my debug message").Create();
// var logger = LogEntryFactory.AsInfo().WithMessage("my info message").Create();
// var logger = LogEntryFactory.AsWarning().WithMessage("my warning message").Create();
// var logger = LogEntryFactory.AsError().WithMessage("my error message").Create();
```

## FluentOptions

An enumeration that provides options for controlling the generation behavior:

- `None`: No special options
- `NoCreateMethod`: Prevents generation of the `Create()` method, causing immediate constructor invocation when all parameters are resolved

## Usage

1. Install this package in your project
2. Apply the appropriate attributes to your classes and constructors
3. Build your project to generate the fluent builder classes

## Requirements

- .NET Standard 2.0 or higher

## License

This package is part of the Motiv project. Please refer to the main project for licensing information.
