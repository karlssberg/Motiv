namespace Motiv.ExpressionTreeProposition;

/// <summary>
/// Provides hints to the serialization process.
/// </summary>
public static class Display
{
    /// <summary>
    /// Instructs the serialization process to serialize the value instead of the expression.
    /// </summary>
    /// <param name="value">The value of the operation</param>
    /// <typeparam name="T">The type of the value to perform the operation upon</typeparam>
    /// <returns>the value unchanged, and without side effects</returns>
    public static T AsValue<T>(T value)
    {
        return value;
    }
}
