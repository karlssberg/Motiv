namespace Motiv.ExpressionTrees;

/// <summary>
/// Provides hints to the serialization process.
/// </summary>
public static class Serialize
{
    /// <summary>
    /// Instructs the serialization process to serialize the value instead of the name.
    /// </summary>
    /// <param name="value">The value of the operation</param>
    /// <typeparam name="T">The type of the value to perform the operation upon</typeparam>
    /// <returns></returns>
    public static T AsValue<T>(T value)
    {
        return value;
    }
}
