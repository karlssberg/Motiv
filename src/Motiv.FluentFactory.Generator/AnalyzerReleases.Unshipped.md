; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MFFG0001 | FluentFactory | Error | Unreachable fluent constructor
MFFG0002 | FluentFactory | Warning | Multiple fluent method contains superseded method
MFFG0003 | FluentFactory | Warning | Fluent method template not compatible
MFFG0004 | FluentFactory | Error | All fluent method template incompatible
MFFG0005 | FluentFactory | Error | Fluent method template not static
MFFG0006 | FluentFactory | Info | Fluent method template superseded
MFFG0007 | FluentFactory | Error | Invalid CreateMethodName
MFFG0008 | FluentFactory | Error | Duplicate CreateMethodName
MFFG0009 | FluentFactory | Error | FluentConstructor target type missing FluentFactory attribute
MFFG0010 | FluentFactory | Error | CreateMethodName specified with NoCreateMethod option
