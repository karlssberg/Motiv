using System.Runtime.InteropServices;

namespace Karlssberg.Motiv;

public static class WrapException
{
    internal static  TResult IfIsSatisfiedByInvocationFails<TModel, TMetadata, TResult>(
        SpecificationBase<TModel, TMetadata> specification,
        Func<TResult> func) =>
        IfIsSatisfiedByInvocationFails<TModel, object?, TMetadata, TResult>(specification, null, func);

    internal static TResult IfIsSatisfiedByInvocationFails<TModel, TModelUnderlying, TMetadata, TResult>(
        SpecificationBase<TModel, TMetadata> specification, 
        SpecificationBase<TModelUnderlying, TMetadata>? underlyingSpecification,
        Func<TResult> func)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            if (ex is SpecificationException
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

            var message = GetErrorMessageForIsSatisfiedByCall(specification, underlyingSpecification, ex);
            throw new SpecificationException(message, ex);
        }
    }

    internal static TResult IfCallbackInvocationFails<TModel, TMetadata, TResult>(
        SpecificationBase<TModel, TMetadata> specification,
        Func<TResult> callback,
        string callbackName)
    {
        try
        {
            return callback();
        }
        catch (Exception ex)
        {
            if (ex is SpecificationException
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
            
            var message = CreateErrorMessageForFailedCallbackInvocation(specification, callbackName, ex);
            throw new SpecificationException(message, ex);
        }
    }

    private static string ConvertToPrettyTypeName<TModel, TMetadata>(SpecificationBase<TModel, TMetadata> specification) 
    {
        var specificationType = specification.GetType();
        var genericArgs = specificationType.GetGenericArguments();
        
        var nameParts = specificationType.Name.Split('`');
        
        var truncatedName = nameParts.First();
        
        return genericArgs.Length switch
        {
            1 => $"{truncatedName}<{genericArgs.First().Name}>",
            2 => $"{truncatedName}<{genericArgs.First().Name}, {genericArgs.Last().Name}>",
            _ => specificationType.Name
        };
    }

    private static string GetErrorMessageForIsSatisfiedByCall<TModel, TModelUnderlying, TMetadata>(
        SpecificationBase<TModel, TMetadata> specification,
        SpecificationBase<TModelUnderlying, TMetadata>? underlyingSpecification,
        Exception ex)
    {
        const string vowels = "AEIOU";
        var exceptionTypeName = ex.GetType().Name;
        var article = vowels.Contains(exceptionTypeName[0]) ? "An" : "A";
        var descriptionPhrase = GetDescriptionPhrase(specification, underlyingSpecification);
            
        var message =  string.IsNullOrWhiteSpace(ex.Message)
            ? $"{article} '{exceptionTypeName}' was thrown while evaluating the specification {descriptionPhrase}."
            : $"{article} '{exceptionTypeName}' was thrown with the message '{ex.Message}' while evaluating the specification {descriptionPhrase}.";
        return message;
    }

    private static string GetDescriptionPhrase<TModel, TModelUnderlying, TMetadata>(
        SpecificationBase<TModel, TMetadata> specification, 
        SpecificationBase<TModelUnderlying, TMetadata>? underlyingSpecification)
    {
        return underlyingSpecification == null
            ? DescribeType(specification) 
            : $"{DescribeType(underlyingSpecification)} encapsulated by the specification {DescribeType(specification)}";
    }

    private static string DescribeType<TModel, TMetadata>(SpecificationBase<TModel, TMetadata> spec) =>
        $"{ConvertToPrettyTypeName(spec)} ({spec.Description})";

    private static string CreateErrorMessageForFailedCallbackInvocation<TModel, TMetadata>(SpecificationBase<TModel, TMetadata> specification, string callerName, Exception ex)
    {
            
        const string vowels = "AEIOU";
        var exceptionTypeName = ex.GetType().Name;
        var article = vowels.Contains(exceptionTypeName[0]) ? "An" : "A";
        var typeDescription = DescribeType(specification);
            
        return  string.IsNullOrWhiteSpace(ex.Message)
            ? $"{article} '{exceptionTypeName}' was thrown while evaluating the '{callerName}' parameter of the specification {typeDescription}."
            : $"{article} '{exceptionTypeName}' was thrown with the message '{ex.Message}' while evaluating the '{callerName}' parameter of the specification {typeDescription}.";
    }
}