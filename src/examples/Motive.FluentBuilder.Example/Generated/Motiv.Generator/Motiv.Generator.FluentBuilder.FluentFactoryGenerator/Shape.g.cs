using System;

public partial class Shape
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Step_0__Shape WithWidth(in int width)
    {
        return new Step_0__Shape(width);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Step_3__Shape WithRadius(in int radius)
    {
        return new Step_3__Shape(radius);
    }
}

public struct Step_0__Shape
{
    private readonly int _width__parameter;
    public Step_0__Shape(in int width)
    {
        this._width__parameter = width;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Step_1__Shape WithHeight(in int height)
    {
        return new Step_1__Shape(this._width__parameter, height);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Square Create()
    {
        return new Square(this._width__parameter);
    }
}

public struct Step_1__Shape
{
    private readonly int _width__parameter;
    private readonly int _height__parameter;
    public Step_1__Shape(in int width, in int height)
    {
        this._width__parameter = width;
        this._height__parameter = height;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Step_2__Shape WithDepth(in int depth)
    {
        return new Step_2__Shape(this._width__parameter, this._height__parameter, depth);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Rectangle Create()
    {
        return new Rectangle(this._width__parameter, this._height__parameter);
    }
}

public struct Step_2__Shape
{
    private readonly int _width__parameter;
    private readonly int _height__parameter;
    private readonly int _depth__parameter;
    public Step_2__Shape(in int width, in int height, in int depth)
    {
        this._width__parameter = width;
        this._height__parameter = height;
        this._depth__parameter = depth;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Cuboid Create()
    {
        return new Cuboid(this._width__parameter, this._height__parameter, this._depth__parameter);
    }
}

public struct Step_3__Shape
{
    private readonly int _radius__parameter;
    public Step_3__Shape(in int radius)
    {
        this._radius__parameter = radius;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Circle Create()
    {
        return new Circle(this._radius__parameter);
    }
}