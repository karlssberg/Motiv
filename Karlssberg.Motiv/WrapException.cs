using System.Runtime.InteropServices;

namespace Karlssberg.Motiv;

internal static class WrapException
{
    internal static TResult CatchFuncExceptionOnBehalfOfSpecType<TModel, TMetadata, TResult>(
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

    private static string CreateErrorMessageForPredicateExceptionOnBehalfOfSpecType<TModel>(
        SpecBase<TModel> underlyingSpec,
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