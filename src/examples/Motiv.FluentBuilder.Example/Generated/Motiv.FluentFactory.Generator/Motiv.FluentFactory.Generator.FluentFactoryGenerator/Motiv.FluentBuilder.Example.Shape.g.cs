namespace Motiv.FluentBuilder.Example
{
    public partial class Shape
    {
        /// <summary>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Cuboid{T}"/>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Diamond{T}"/>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Rectangle{T}"/>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Square{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static Step_0__Motiv_FluentBuilder_Example_Shape<T> WithWidth<T>(in T width)
            where T : System.Numerics.INumber<T>
        {
            return new Step_0__Motiv_FluentBuilder_Example_Shape<T>(width);
        }

        /// <summary>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Circle{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static Step_3__Motiv_FluentBuilder_Example_Shape<T> WithRadius<T>(in T radius)
            where T : System.Numerics.INumber<T>
        {
            return new Step_3__Motiv_FluentBuilder_Example_Shape<T>(radius);
        }
    }

    /// <summary>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Cuboid{T}"/>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Diamond{T}"/>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Rectangle{T}"/>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Square{T}"/>
    /// </summary>
    public struct Step_0__Motiv_FluentBuilder_Example_Shape<T> where T : System.Numerics.INumber<T>
    {
        private readonly T _width__parameter;
        internal Step_0__Motiv_FluentBuilder_Example_Shape(in T width)
        {
            this._width__parameter = width;
        }

        /// <summary>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Cuboid{T}"/>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Diamond{T}"/>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Rectangle{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_1__Motiv_FluentBuilder_Example_Shape<T> WithHeight(in T height)
        {
            return new Step_1__Motiv_FluentBuilder_Example_Shape<T>(this._width__parameter, height);
        }

        /// <summary>
        /// Creates a new instance using constructor Motiv.FluentBuilder.Example.Square<T>.Square(T Width).
        ///
        ///     <seealso cref="Motiv.FluentBuilder.Example.Square{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Square<T> CreateSquare()
        {
            return new Square<T>(this._width__parameter);
        }
    }

    /// <summary>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Cuboid{T}"/>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Diamond{T}"/>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Rectangle{T}"/>
    /// </summary>
    public struct Step_1__Motiv_FluentBuilder_Example_Shape<T> where T : System.Numerics.INumber<T>
    {
        private readonly T _width__parameter;
        private readonly T _height__parameter;
        internal Step_1__Motiv_FluentBuilder_Example_Shape(in T width, in T height)
        {
            this._width__parameter = width;
            this._height__parameter = height;
        }

        /// <summary>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Cuboid{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_2__Motiv_FluentBuilder_Example_Shape<T> WithDepth(in T depth)
        {
            return new Step_2__Motiv_FluentBuilder_Example_Shape<T>(this._width__parameter, this._height__parameter, depth);
        }

        /// <summary>
        /// Creates a new instance using constructor Motiv.FluentBuilder.Example.Rectangle<T>.Rectangle(T Width, T Height).
        ///
        ///     <seealso cref="Motiv.FluentBuilder.Example.Rectangle{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Rectangle<T> CreateRectangle()
        {
            return new Rectangle<T>(this._width__parameter, this._height__parameter);
        }

        /// <summary>
        /// Creates a new instance using constructor Motiv.FluentBuilder.Example.Diamond<T>.Diamond(T Width, T Height).
        ///
        ///     <seealso cref="Motiv.FluentBuilder.Example.Diamond{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Diamond<T> CreateDiamond()
        {
            return new Diamond<T>(this._width__parameter, this._height__parameter);
        }
    }

    /// <summary>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Cuboid{T}"/>
    /// </summary>
    public struct Step_2__Motiv_FluentBuilder_Example_Shape<T> where T : System.Numerics.INumber<T>
    {
        private readonly T _width__parameter;
        private readonly T _height__parameter;
        private readonly T _depth__parameter;
        internal Step_2__Motiv_FluentBuilder_Example_Shape(in T width, in T height, in T depth)
        {
            this._width__parameter = width;
            this._height__parameter = height;
            this._depth__parameter = depth;
        }

        /// <summary>
        /// Creates a new instance using constructor Motiv.FluentBuilder.Example.Cuboid<T>.Cuboid(T Width, T Height, T Depth).
        ///
        ///     <seealso cref="Motiv.FluentBuilder.Example.Cuboid{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Cuboid<T> CreateCuboid()
        {
            return new Cuboid<T>(this._width__parameter, this._height__parameter, this._depth__parameter);
        }
    }

    /// <summary>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Circle{T}"/>
    /// </summary>
    public struct Step_3__Motiv_FluentBuilder_Example_Shape<T> where T : System.Numerics.INumber<T>
    {
        private readonly T _radius__parameter;
        internal Step_3__Motiv_FluentBuilder_Example_Shape(in T radius)
        {
            this._radius__parameter = radius;
        }

        /// <summary>
        /// Creates a new instance using constructor Motiv.FluentBuilder.Example.Circle<T>.Circle(T Radius).
        ///
        ///     <seealso cref="Motiv.FluentBuilder.Example.Circle{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Circle<T> CreateCircle()
        {
            return new Circle<T>(this._radius__parameter);
        }
    }
}