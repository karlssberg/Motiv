using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Karlssberg.Motive;

[Serializable]
public class SpecificationException : Exception
{
    public SpecificationException()
    {
    }

    protected SpecificationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }

    public SpecificationException(string message)
        : base(message)
    {
    }
    
    public SpecificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
    
    private static string GetCaller<TModel, TMetadata>(SpecificationBase<TModel, TMetadata> specification)
    {
        var specificationTypeName = ConvertToPrettyTypeName(specification);
        const string methodName = nameof(SpecificationBase<object, object>.Evaluate);
        
        return $"{specificationTypeName}.{methodName}()";
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

    internal static  TResult WrapThrownExceptions<TModel, TMetadata, TResult>(
        SpecificationBase<TModel, TMetadata> specification,
        Func<TResult> func) =>
        WrapThrownExceptions<TModel, object?, TMetadata, TResult>(specification, null, func);

    internal static TResult WrapThrownExceptions<TModel, TModelUnderlying, TMetadata, TResult>(
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
            
            const string vowels = "AEIOU";
            var exceptionTypeName = ex.GetType().Name;
            var article = vowels.Contains(exceptionTypeName[0]) ? "An" : "A";
            var descriptionPhrase = GetDescriptionPhrase(specification, underlyingSpecification);
            
            var message =  string.IsNullOrWhiteSpace(ex.Message)
                ? $"{article} '{exceptionTypeName}' was thrown while evaluating the specification {descriptionPhrase}."
                : $"{article} '{exceptionTypeName}' was thrown with the message '{ex.Message}' while evaluating the specification {descriptionPhrase}.";
            throw new SpecificationException(message, ex);
        }
    }

    private static string GetDescriptionPhrase<TModel, TModelUnderlying, TMetadata>(
        SpecificationBase<TModel, TMetadata> specification, 
        SpecificationBase<TModelUnderlying, TMetadata>? underlyingSpecification)
    {
        return underlyingSpecification == null
            ? GetDescription(specification) 
            : $"{GetDescription(underlyingSpecification)} encapsulated by the specification {GetDescription(specification)}";

        string GetDescription<T>(SpecificationBase<T, TMetadata> spec) =>
            $"{ConvertToPrettyTypeName(spec)} ({spec.Description})";
    }
}