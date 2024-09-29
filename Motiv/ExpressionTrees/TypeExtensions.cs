using System.Reflection;

namespace Motiv.ExpressionTrees;

internal static class TypeExtensions
{
    internal static string ToCSharpName(this Type type)
    {
        var typeName = GetTypeKeyword(type);
        if (!type.IsGenericType)
        {
            return typeName;
        }

        var genericTypeName = typeName.Substring(0, type.Name.IndexOf('`'));
        var genericArguments = type.GetGenericArguments().Select(ToCSharpName);
        return $"{genericTypeName}<{string.Join(", ", genericArguments)}>";
    }


    internal static string ToCSharpName(this MethodInfo method)
    {
        if (!method.IsGenericMethod)
        {
            return method.Name;
        }

        var genericArgs = method
            .GetGenericArguments()
            .Select(ToCSharpName);

        return $"{method.Name}<{string.Join(", ", genericArgs)}>";
    }

    public static string GetTypeKeyword(Type type)
    {
        return type switch
        {
            // Integral types
            _ when type == typeof(sbyte) => "sbyte",
            _ when type == typeof(byte) => "byte",
            _ when type == typeof(short) => "short",
            _ when type == typeof(ushort) => "ushort",
            _ when type == typeof(int) => "int",
            _ when type == typeof(uint) => "uint",
            _ when type == typeof(long) => "long",
            _ when type == typeof(ulong) => "ulong",
            _ when type == typeof(char) => "char",

            // Floating-point types
            _ when type == typeof(float) => "float",
            _ when type == typeof(double) => "double",

            // Decimal type
            _ when type == typeof(decimal) => "decimal",

            // Boolean type
            _ when type == typeof(bool) => "bool",

            // String type
            _ when type == typeof(string) => "string",

            // Object type
            _ when type == typeof(object) => "object",

            // Native-sized integers
            _ when type == typeof(nint) => "nint",
            _ when type == typeof(nuint) => "nuint",

            // Special case for void
            _ when type == typeof(void) => "void",

            // For any other type, return its name
            _ => type.Name
        };
    }


}
