using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.FluentFactory.Generator.Analysis;
using static Motiv.FluentFactory.Generator.FluentFactoryGeneratorOptions;

namespace Motiv.FluentFactory.Generator.Model;

internal static class FluentConstructorValidatorExtensions
{
    public static IEnumerable<Diagnostic> GetDiagnostics(this ImmutableArray<FluentConstructorContext> fluentConstructorContexts)
    {
        return ValidateRootTypeAttributes(fluentConstructorContexts)
            .Concat(ValidateCreateMethodNames(fluentConstructorContexts))
            .Concat(ValidateDuplicateCreateMethodNames(fluentConstructorContexts))
            .Concat(ValidateCreateMethodNameConflicts(fluentConstructorContexts));
    }

    private static IEnumerable<Diagnostic> ValidateRootTypeAttributes(ImmutableArray<FluentConstructorContext> fluentConstructorContexts)
    {
        // Check if the target type has the FluentFactory attribute
        var constructorContexts = fluentConstructorContexts
            .Where(context => !context.RootType.HasAttribute<FluentFactoryAttribute>());

        foreach (var context in constructorContexts)
            yield return Diagnostic.Create(
                FluentFactoryGenerator.FluentConstructorTargetTypeMissingFluentFactory,
                FindRootTypeLocation(context.AttributeData, context),
                context.RootType.ToDisplayString());
    }

    private static IEnumerable<Diagnostic> ValidateCreateMethodNames(ImmutableArray<FluentConstructorContext> fluentConstructorContexts)
    {
        // Check for valid CreateMethodName values
        var constructorContextWithInvalidCreateMethodName = fluentConstructorContexts
            .Where(context =>
            {
                var isFirstCharValid = context.CreateMethodName?.Select(char.IsLetter).FirstOrDefault() ?? true;
                var areRemainingCharsValid = context.CreateMethodName?.Skip(1).All(char.IsLetterOrDigit) ?? true;
                return !(isFirstCharValid && areRemainingCharsValid);
            });

        foreach (var context in constructorContextWithInvalidCreateMethodName)
        {
            yield return Diagnostic.Create(
                FluentFactoryGenerator.InvalidCreateMethodName,
                FindCreateMethodNameArgumentLocation(context));
        }
    }

    private static IEnumerable<Diagnostic> ValidateDuplicateCreateMethodNames(ImmutableArray<FluentConstructorContext> fluentConstructorContexts)
    {
        // Check for duplicate CreateMethodName values within the same type
        var duplicateGroups = fluentConstructorContexts
            .Where(context => !string.IsNullOrEmpty(context.CreateMethodName))
            .GroupBy(context => new
                { context.CreateMethodName, TypeName = context.Constructor.ContainingType.ToDisplayString() })
            .Where(group => group.Count() > 1);

        foreach (var context in duplicateGroups.SelectMany(c => c))
        {
            yield return Diagnostic.Create(
                FluentFactoryGenerator.DuplicateCreateMethodName,
                FindCreateMethodNameArgumentLocation(context));
        }
    }

    private static IEnumerable<Diagnostic> ValidateCreateMethodNameConflicts(ImmutableArray<FluentConstructorContext> fluentConstructorContexts)
    {
        // Check for NoCreateMethod option used with CreateMethodName
        var conflictedMethodNameFluentConstructorContexts = fluentConstructorContexts.AsEnumerable().Where(context =>
            context.Options.HasFlag(NoCreateMethod)
            && !string.IsNullOrEmpty(context.CreateMethodName));

        foreach (var context in conflictedMethodNameFluentConstructorContexts)
        {
            yield return Diagnostic.Create(
                FluentFactoryGenerator.CreateMethodNameWithNoCreateMethod,
                context.AttributeData.ApplicationSyntaxReference?.GetSyntax() switch
                {
                    AttributeSyntax attributeSyntax => attributeSyntax.GetLocation(),
                    _ => Location.None,
                });
        }
    }

    private static Location FindRootTypeLocation(AttributeData? fluentConstructorAttribute, FluentConstructorContext context)
    {
        Location location;
        if (fluentConstructorAttribute?.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax attributeSyntax)
        {
            // Find the typeof argument that references the root type
            var typeofArg = attributeSyntax.ArgumentList?.Arguments
                .OfType<AttributeArgumentSyntax>()
                .FirstOrDefault(arg => arg.Expression is TypeOfExpressionSyntax);

            location = typeofArg != null
                ? typeofArg.Expression.GetLocation()
                : attributeSyntax.GetLocation();
        }
        else
        {
            location = context.Constructor.Locations.FirstOrDefault() ?? Location.None;
        }

        return location;
    }

    private static Location FindCreateMethodNameArgumentLocation(FluentConstructorContext context)
    {
        Location location;
        if (context.AttributeData.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax attributeSyntax)
        {
            // Find the CreateMethodName named argument
            var createMethodNameArg = attributeSyntax.ArgumentList?.Arguments
                .OfType<AttributeArgumentSyntax>()
                .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.ValueText == "CreateMethodName");

            location = createMethodNameArg != null
                ? createMethodNameArg.GetLocation()
                : attributeSyntax.GetLocation();
        }
        else
        {
            location = context.Constructor.Locations.FirstOrDefault() ?? Location.None;
        }

        return location;
    }
}
