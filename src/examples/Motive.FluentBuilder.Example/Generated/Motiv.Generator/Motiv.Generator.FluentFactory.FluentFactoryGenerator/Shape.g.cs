using System;

public partial class Shape
{
    /// <summary>
    ///     <seealso cref="Cuboid"/>
    ///     <seealso cref="Rectangle"/>
    ///     <seealso cref="Square"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Step_0__Shape WithWidth(in int width)
    {
        return new Step_0__Shape(width);
    }

    /// <summary>
    ///     <seealso cref="Circle"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Step_3__Shape WithRadius(in int radius)
    {
        return new Step_3__Shape(radius);
    }
}

/// <summary>
///     <seealso cref="Cuboid"/>
///     <seealso cref="Rectangle"/>
///     <seealso cref="Square"/>
/// </summary>
public struct Step_0__Shape
{
    private readonly int _width__parameter;
    internal Step_0__Shape(in int width)
    {
        this._width__parameter = width;
    }

    /// <summary>
    ///     <seealso cref="Cuboid"/>
    ///     <seealso cref="Rectangle"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Step_1__Shape WithHeight(in int height)
    {
        return new Step_1__Shape(this._width__parameter, height);
    }

    /// <summary>
    /// Creates a new instance using constructor Square.Square(int Width).
    ///
    ///     <seealso cref="Square"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Square Create()
    {
        return new Square(this._width__parameter);
    }
}

/// <summary>
///     <seealso cref="Cuboid"/>
///     <seealso cref="Rectangle"/>
/// </summary>
public struct Step_1__Shape
{
    private readonly int _width__parameter;
    private readonly int _height__parameter;
    internal Step_1__Shape(in int width, in int height)
    {
        this._width__parameter = width;
        this._height__parameter = height;
    }

    /// <summary>
    ///     <seealso cref="Cuboid"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Step_2__Shape WithDepth(in int depth)
    {
        return new Step_2__Shape(this._width__parameter, this._height__parameter, depth);
    }

    /// <summary>
    /// Creates a new instance using constructor Rectangle.Rectangle(int Width, int Height).
    ///
    ///     <seealso cref="Rectangle"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Rectangle Create()
    {
        return new Rectangle(this._width__parameter, this._height__parameter);
    }
}

/// <summary>
///     <seealso cref="Cuboid"/>
/// </summary>
public struct Step_2__Shape
{
    private readonly int _width__parameter;
    private readonly int _height__parameter;
    private readonly int _depth__parameter;
    internal Step_2__Shape(in int width, in int height, in int depth)
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

/// <summary>
///     <seealso cref="Circle"/>
/// </summary>
public struct Step_3__Shape
{
    private readonly int _radius__parameter;
    internal Step_3__Shape(in int radius)
    {
        this._radius__parameter = radius;
    }

    /// <summary>
    /// Creates a new instance using constructor Circle.Circle(int Radius).
    ///
    ///     <seealso cref="Circle"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Circle Create()
    {
        return new Circle(this._radius__parameter);
    }
}