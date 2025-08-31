public partial class Shape<T>
    where T : System.Numerics.INumber<T>
{
    /// <summary>
    ///     <seealso cref="Cuboid{T}"/>
    ///     <seealso cref="Diamond{T}"/>
    ///     <seealso cref="Rectangle{T}"/>
    ///     <seealso cref="Square{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Step_0__Shape____<T> WithWidth(in T width)
    {
        return new Step_0__Shape____<T>(width);
    }

    /// <summary>
    ///     <seealso cref="Circle{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Step_3__Shape____<T> WithRadius(in T radius)
    {
        return new Step_3__Shape____<T>(radius);
    }
}

/// <summary>
///     <seealso cref="Cuboid{T}"/>
///     <seealso cref="Diamond{T}"/>
///     <seealso cref="Rectangle{T}"/>
///     <seealso cref="Square{T}"/>
/// </summary>
public struct Step_0__Shape____<T> where T : System.Numerics.INumber<T>
{
    private readonly T _width__parameter;
    internal Step_0__Shape____(in T width)
    {
        this._width__parameter = width;
    }

    /// <summary>
    ///     <seealso cref="Cuboid{T}"/>
    ///     <seealso cref="Diamond{T}"/>
    ///     <seealso cref="Rectangle{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Step_1__Shape____<T> WithHeight(in T height)
    {
        return new Step_1__Shape____<T>(this._width__parameter, height);
    }

    /// <summary>
    /// Creates a new instance using constructor Square<T>.Square(T Width).
    ///
    ///     <seealso cref="Square{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Square<T> CreateSquare()
    {
        return new Square<T>(this._width__parameter);
    }
}

/// <summary>
///     <seealso cref="Cuboid{T}"/>
///     <seealso cref="Diamond{T}"/>
///     <seealso cref="Rectangle{T}"/>
/// </summary>
public struct Step_1__Shape____<T> where T : System.Numerics.INumber<T>
{
    private readonly T _width__parameter;
    private readonly T _height__parameter;
    internal Step_1__Shape____(in T width, in T height)
    {
        this._width__parameter = width;
        this._height__parameter = height;
    }

    /// <summary>
    ///     <seealso cref="Cuboid{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Step_2__Shape____<T> WithDepth(in T depth)
    {
        return new Step_2__Shape____<T>(this._width__parameter, this._height__parameter, depth);
    }

    /// <summary>
    /// Creates a new instance using constructor Rectangle<T>.Rectangle(T Width, T Height).
    ///
    ///     <seealso cref="Rectangle{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Rectangle<T> CreateRectangle()
    {
        return new Rectangle<T>(this._width__parameter, this._height__parameter);
    }

    /// <summary>
    /// Creates a new instance using constructor Diamond<T>.Diamond(T Width, T Height).
    ///
    ///     <seealso cref="Diamond{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Diamond<T> CreateDiamond()
    {
        return new Diamond<T>(this._width__parameter, this._height__parameter);
    }
}

/// <summary>
///     <seealso cref="Cuboid{T}"/>
/// </summary>
public struct Step_2__Shape____<T> where T : System.Numerics.INumber<T>
{
    private readonly T _width__parameter;
    private readonly T _height__parameter;
    private readonly T _depth__parameter;
    internal Step_2__Shape____(in T width, in T height, in T depth)
    {
        this._width__parameter = width;
        this._height__parameter = height;
        this._depth__parameter = depth;
    }

    /// <summary>
    /// Creates a new instance using constructor Cuboid<T>.Cuboid(T Width, T Height, T Depth).
    ///
    ///     <seealso cref="Cuboid{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Cuboid<T> CreateCuboid()
    {
        return new Cuboid<T>(this._width__parameter, this._height__parameter, this._depth__parameter);
    }
}

/// <summary>
///     <seealso cref="Circle{T}"/>
/// </summary>
public struct Step_3__Shape____<T> where T : System.Numerics.INumber<T>
{
    private readonly T _radius__parameter;
    internal Step_3__Shape____(in T radius)
    {
        this._radius__parameter = radius;
    }

    /// <summary>
    /// Creates a new instance using constructor Circle<T>.Circle(T Radius).
    ///
    ///     <seealso cref="Circle{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Circle<T> CreateCircle()
    {
        return new Circle<T>(this._radius__parameter);
    }
}