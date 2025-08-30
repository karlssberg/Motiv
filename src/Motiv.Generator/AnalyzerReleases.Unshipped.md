; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MOTIV001 | FluentFactory | Error | Unreachable fluent constructor
MOTIV002 | FluentFactory | Warning | Multiple fluent method contains superseded method
MOTIV003 | FluentFactory | Warning | Fluent method template not compatible
MOTIV004 | FluentFactory | Error | All fluent method template incompatible
MOTIV005 | FluentFactory | Error | Fluent method template not static
MOTIV006 | FluentFactory | Info | Fluent method template superseded
