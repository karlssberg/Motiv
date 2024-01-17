﻿using System.Runtime.InteropServices;

namespace Karlssberg.Motiv;

internal static class WrapException
{
    internal static  TResult IfIsSatisfiedByInvocationFails<TModel, TMetadata, TResult>(
        SpecBase<TModel, TMetadata> spec,
        Func<TResult> func) =>
        IfIsSatisfiedByInvocationFails<TModel, object?, TMetadata, object?, TResult>(spec, null, func);
    
    internal static TResult IfIsSatisfiedByInvocationFails<TModel, TUnderlyingModel, TMetadata, TUnderlyingMetadata, TResult>(
        SpecBase<TModel, TMetadata> spec, 
        SpecBase<TUnderlyingModel, TUnderlyingMetadata> underlyingSpecification,
        Func<TResult> func)
    {
        try
        {
            return func();
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

            var message = GetErrorMessageForIsSatisfiedByCall(spec, underlyingSpecification, ex);
            throw new SpecException(message, ex);
        }
    }

    internal static TResult IfCallbackInvocationFails<TModel, TMetadata, TResult>(
        SpecBase<TModel, TMetadata> spec,
        Func<TResult> callback,
        string callbackName)
    {
        try
        {
            return callback();
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
            
            var message = CreateErrorMessageForFailedCallbackInvocation(spec, callbackName, ex);
            throw new SpecException(message, ex);
        }
    }

    private static string ConvertToPrettyTypeName<TModel, TMetadata>(SpecBase<TModel, TMetadata> spec) 
    {
        var specificationType = spec.GetType();
        var genericArgs = specificationType.GetGenericArguments();
        
        var nameParts = specificationType.Name.Split('`');
        
        var truncatedName = nameParts.First();
        
        var serializedGenericArgs = string.Join(", ", genericArgs.Select(arg => arg.Name));
        
        return $"{truncatedName}<{serializedGenericArgs}>";
    }

    private static string GetErrorMessageForIsSatisfiedByCall<TModel, TUnderlyingModel, TMetadata, TUnderlyingMetadata>(
        SpecBase<TModel, TMetadata> spec,
        SpecBase<TUnderlyingModel, TUnderlyingMetadata>? underlyingSpecification,
        Exception ex)
    {
        const string vowels = "AEIOU";
        var exceptionTypeName = ex.GetType().Name;
        var article = vowels.Contains(exceptionTypeName[0]) ? "An" : "A";
        var descriptionPhrase = GetDescriptionPhrase(spec, underlyingSpecification);
            
        var message =  string.IsNullOrWhiteSpace(ex.Message)
            ? $"{article} '{exceptionTypeName}' was thrown while evaluating the specification {descriptionPhrase}."
            : $"{article} '{exceptionTypeName}' was thrown with the message '{ex.Message}' while evaluating the specification {descriptionPhrase}.";
        return message;
    }

    private static string GetDescriptionPhrase<TModel, TUnderlyingModel, TMetadata, TUnderlyingMetadata>(
        SpecBase<TModel, TMetadata> spec, 
        SpecBase<TUnderlyingModel, TUnderlyingMetadata>? underlyingSpecification)
    {
        return underlyingSpecification switch
        {
            null => DescribeType(spec),
            _ => $"{DescribeType(underlyingSpecification)} encapsulated by the specification {DescribeType(spec)}"
        };
    }

    private static string DescribeType<TModel, TMetadata>(SpecBase<TModel, TMetadata> spec) =>
        $"{ConvertToPrettyTypeName(spec)} ({spec.Description})";

    private static string CreateErrorMessageForFailedCallbackInvocation<TModel, TMetadata>(
        SpecBase<TModel, TMetadata> spec, 
        string callerName, 
        Exception ex)
    {
            
        const string vowels = "AEIOU";
        var exceptionTypeName = ex.GetType().Name;
        var article = vowels.Contains(exceptionTypeName[0]) ? "An" : "A";
        var typeDescription = DescribeType(spec);
            
        return  string.IsNullOrWhiteSpace(ex.Message)
            ? $"{article} '{exceptionTypeName}' was thrown while evaluating the '{callerName}' parameter of the specification {typeDescription}."
            : $"{article} '{exceptionTypeName}' was thrown with the message '{ex.Message}' while evaluating the '{callerName}' parameter of the specification {typeDescription}.";
    }
}