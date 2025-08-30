using System;

public partial record Cuboid
{
    /// <summary>
    ///     <seealso cref="Cuboid"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Step_0__Cuboid WithWidth(in int width)
    {
        return new Step_0__Cuboid(width);
    }
}

/// <summary>
///     <seealso cref="Cuboid"/>
/// </summary>
public struct Step_0__Cuboid
{
    private readonly int _width__parameter;
    internal Step_0__Cuboid(in int width)
    {
        this._width__parameter = width;
    }

    /// <summary>
    ///     <seealso cref="Cuboid"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Step_1__Cuboid WithHeight(in int height)
    {
        return new Step_1__Cuboid(this._width__parameter, height);
    }
}

/// <summary>
///     <seealso cref="Cuboid"/>
/// </summary>
public struct Step_1__Cuboid
{
    private readonly int _width__parameter;
    private readonly int _height__parameter;
    internal Step_1__Cuboid(in int width, in int height)
    {
        this._width__parameter = width;
        this._height__parameter = height;
    }

    /// <summary>
    ///     <seealso cref="Cuboid"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Step_2__Cuboid WithDepth(in int depth)
    {
        return new Step_2__Cuboid(this._width__parameter, this._height__parameter, depth);
    }
}

/// <summary>
///     <seealso cref="Cuboid"/>
/// </summary>
public struct Step_2__Cuboid
{
    private readonly int _width__parameter;
    private readonly int _height__parameter;
    private readonly int _depth__parameter;
    internal Step_2__Cuboid(in int width, in int height, in int depth)
    {
        this._width__parameter = width;
        this._height__parameter = height;
        this._depth__parameter = depth;
    }

    /// <summary>
    /// Creates a new instance using constructor Cuboid.Cuboid(int Width, int Height, int Depth).
    ///
    ///     <seealso cref="Cuboid"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Cuboid Create()
    {
        return new Cuboid(this._width__parameter, this._height__parameter, this._depth__parameter);
    }
}