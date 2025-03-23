using System;

public partial record Square
{
    /// <summary>
    /// Candidate constructor types:
    ///     <seealso cref="Square"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Step_0__Square WithWidth(in int width)
    {
        return new Step_0__Square(width);
    }
}

/// <summary>
/// Candidate constructor types:
///     <seealso cref="Square"/>
/// </summary>
public struct Step_0__Square
{
    private readonly int _width__parameter;
    public Step_0__Square(in int width)
    {
        this._width__parameter = width;
    }

    /// <summary>
    /// Candidate constructor types:
    ///     <seealso cref="Square"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Square Create()
    {
        return new Square(this._width__parameter);
    }
}