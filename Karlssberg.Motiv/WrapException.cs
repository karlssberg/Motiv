using System.Runtime.InteropServices;

namespace Karlssberg.Motiv;

internal static class WrapException
{
    internal static TResult IfIsSatisfiedByInvocationFails<TModel, TMetadata, TResult>(
        SpecBase<TModel, TMetadata> spec,
        Func<TResult> func) =>
        IfIsSatisfiedByInvocationFails<TModel, object?, TMetadata, object?, TResult>(spec, null, func);

    internal static TResult IfIsSatisfiedByInvocationFails<TModel, TUnderlyingModel, TMetadata, TUnderlyingMetadata, TResult>(
        SpecBase<TModel, TMetadata> spec,
        SpecBase<TUnderlyingModel, TUnderlyingMetadata>? underlyingSpecification,
        Func<TResult> func)
    {
        return TryCatchThrow(func, ex =>
        {
            var message = GetErrorMessageForIsSatisfiedByCall(spec, underlyingSpecification, ex);
            return new SpecException(message, ex);
        });
    }
    internal static BooleanResultBase<TMetadata> IsSatisfiedByOrWrapException<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        TModel model)
    {
        return TryCatchThrow(() => spec.IsSatisfiedBy(model), ex =>
        {
            var message = GetErrorMessageForIsSatisfiedByCall(spec, ex);
            return new SpecException(message, ex);
        });
    }

    internal static TResult CatchPredicateExceptionOnBehalfOfSpecType<TModel, TMetadata, TResult>(
        SpecBase<TModel, TMetadata> underlyingSpec,
        Func<TResult> tryThisFn,
        string callbackName)
    {
        return TryCatchThrow<TResult, SpecException>(tryThisFn, ex =>
        {
            var message = CreateErrorMessageForPredicateExceptionOnBehalfOfSpecType(underlyingSpec, callbackName, ex);
            throw new SpecException(message, ex);
        });
    }

    private static string   GetErrorMessageForIsSatisfiedByCall<TModel, TUnderlyingModel, TMetadata, TUnderlyingMetadata>(
        SpecBase<TModel, TMetadata> spec,
        SpecBase<TUnderlyingModel, TUnderlyingMetadata>? underlyingSpecification,
        Exception ex)
    {
        const string vowels = "AEIOU";
        var exceptionTypeName = ex.GetType().Name;
        var article = vowels.Contains(exceptionTypeName[0]) ? "An" : "A";
        var descriptionPhrase = GetDescriptionPhrase(spec, underlyingSpecification);

        var message = string.IsNullOrWhiteSpace(ex.Message)
            ? $"{article} '{exceptionTypeName}' was thrown while evaluating the specification {descriptionPhrase}."
            : $"{article} '{exceptionTypeName}' was thrown with the message '{ex.Message}' while evaluating the specification {descriptionPhrase}.";
        return message;
    }
    
    private static string   GetErrorMessageForIsSatisfiedByCall<TModel, TMetadata>(
        SpecBase<TModel, TMetadata> spec,
        Exception ex)
    {
        const string vowels = "AEIOU";
        var exceptionTypeName = ex.GetType().Name;
        var article = vowels.Contains(exceptionTypeName[0]) ? "An" : "A";
        var descriptionPhrase = DescribeType(spec);

        return string.IsNullOrWhiteSpace(ex.Message)
            ? $"{article} '{exceptionTypeName}' was thrown while evaluating the specification {descriptionPhrase}."
            : $"{article} '{exceptionTypeName}' was thrown with the message '{ex.Message}' while evaluating the specification {descriptionPhrase}.";
    }

    private static string CreateErrorMessageForPredicateExceptionOnBehalfOfSpecType<TModel, TMetadata>(
        SpecBase<TModel, TMetadata> underlyingSpec,
        string callerName,
        Exception ex)
    {
        const string vowels = "AEIOU";
        var exceptionTypeName = ex.GetType().Name;
        var article = vowels.Contains(exceptionTypeName[0]) ? "An" : "A";
        var genericParams = SerializedGenericArguments(underlyingSpec.GetType());

        return string.IsNullOrWhiteSpace(ex.Message)
            ? $"{article} '{exceptionTypeName}' was thrown while evaluating the '{callerName}' parameter that was supplied to Spec<{genericParams}> (aka '{underlyingSpec.Description}')."
            : $"{article} '{exceptionTypeName}' was thrown with the message '{ex.Message}' while evaluating the '{callerName}' parameter that was supplied to Spec<{genericParams}> (aka '{underlyingSpec.Description}').";
    }

    private static string GetDescriptionPhrase<TModel, TUnderlyingModel, TMetadata, TUnderlyingMetadata>(
        SpecBase<TModel, TMetadata> spec,
        SpecBase<TUnderlyingModel, TUnderlyingMetadata>? underlyingSpecification)
    {
        return underlyingSpecification switch
        {
            IHaveUnderlyingSpec<TModel, TUnderlyingMetadata> hasUnderlying => FindNonUnderlyingSpecDescription(hasUnderlying.UnderlyingSpec),
            null => DescribeType(spec),
            _ => $"{DescribeType(underlyingSpecification)}. The specification is expressed as '{spec.Description}'"
        };
    }
    
    private static string FindNonUnderlyingSpecDescription<TModel, TUnderlyingMetadata>(SpecBase<TModel, TUnderlyingMetadata> underlyingSpec)
    {
        return underlyingSpec switch
        {
            IHaveUnderlyingSpec<TModel, TUnderlyingMetadata> hasUnderlyingSpec => FindNonUnderlyingSpecDescription(hasUnderlyingSpec.UnderlyingSpec),
            _ => DescribeType(underlyingSpec)
        };
    }

    private static string DescribeType<TModel, TMetadata>(SpecBase<TModel, TMetadata> spec) => ConvertToPrettyTypeName(spec.GetType());

    private static string ConvertToPrettyTypeName(Type specificationType)
    {
        if (!specificationType.IsGenericType)
            return specificationType.Name;

        var nameParts = specificationType.Name.Split('`');

        var truncatedName = nameParts.First();

        var serializedGenericArgs = SerializedGenericArguments(specificationType);

        return $"{truncatedName}<{serializedGenericArgs}>";
    }

    private static string SerializedGenericArguments(Type specificationType)
    {
        if (!specificationType.IsGenericType)
            return string.Empty;

        var genericArgs = specificationType.GetGenericArguments();
        return string.Join(", ", genericArgs.Select(arg => arg.Name));
    }

    private static TResult TryCatchThrow<TResult, TException>(
        Func<TResult> tryThisFn, 
        Func<Exception, TException> throwThisFn)
        where TException : Exception
    {
        try
        {
            return tryThisFn();
        }
        catch (Exception ex)
        {
            if (ex is SpecException
                or OutOfMemoryException
                or StackOverflowException
                or AccessViolationException
                or AppDomainUnloadedException
                or SEHException
                or BadImageFormatException
                or InvalidProgramException)
            {
                throw;
            }

            throw throwThisFn(ex);
        }
    }
}