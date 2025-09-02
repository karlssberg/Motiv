namespace Motiv.FluentBuilder.Example
{
    public static partial class Line
    {
        /// <summary>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Line2D{T}"/>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Line3D{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static Step_0__Motiv_FluentBuilder_Example_Line<T> X<T>(in T x)
            where T : System.Numerics.INumber<T>
        {
            return new Step_0__Motiv_FluentBuilder_Example_Line<T>(x);
        }
    }

    /// <summary>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Line2D{T}"/>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Line3D{T}"/>
    /// </summary>
    public struct Step_0__Motiv_FluentBuilder_Example_Line<T> where T : System.Numerics.INumber<T>
    {
        private readonly T _x__parameter;
        internal Step_0__Motiv_FluentBuilder_Example_Line(in T x)
        {
            this._x__parameter = x;
        }

        /// <summary>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Line2D{T}"/>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Line3D{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_1__Motiv_FluentBuilder_Example_Line<T> Y(in T y)
        {
            return new Step_1__Motiv_FluentBuilder_Example_Line<T>(this._x__parameter, y);
        }
    }

    /// <summary>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Line2D{T}"/>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Line3D{T}"/>
    /// </summary>
    public struct Step_1__Motiv_FluentBuilder_Example_Line<T> where T : System.Numerics.INumber<T>
    {
        private readonly T _x__parameter;
        private readonly T _y__parameter;
        internal Step_1__Motiv_FluentBuilder_Example_Line(in T x, in T y)
        {
            this._x__parameter = x;
            this._y__parameter = y;
        }

        /// <summary>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Line3D{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_2__Motiv_FluentBuilder_Example_Line<T> Z(in T z)
        {
            return new Step_2__Motiv_FluentBuilder_Example_Line<T>(this._x__parameter, this._y__parameter, z);
        }

        /// <summary>
        /// Creates a new instance using constructor Motiv.FluentBuilder.Example.Line2D<T>.Line2D(T X, T Y).
        ///
        ///     <seealso cref="Motiv.FluentBuilder.Example.Line2D{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Line2D<T> Create()
        {
            return new Line2D<T>(this._x__parameter, this._y__parameter);
        }
    }

    /// <summary>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Line3D{T}"/>
    /// </summary>
    public struct Step_2__Motiv_FluentBuilder_Example_Line<T> where T : System.Numerics.INumber<T>
    {
        private readonly T _x__parameter;
        private readonly T _y__parameter;
        private readonly T _z__parameter;
        internal Step_2__Motiv_FluentBuilder_Example_Line(in T x, in T y, in T z)
        {
            this._x__parameter = x;
            this._y__parameter = y;
            this._z__parameter = z;
        }

        /// <summary>
        /// Creates a new instance using constructor Motiv.FluentBuilder.Example.Line3D<T>.Line3D(T X, T Y, T Z).
        ///
        ///     <seealso cref="Motiv.FluentBuilder.Example.Line3D{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Line3D<T> Create()
        {
            return new Line3D<T>(this._x__parameter, this._y__parameter, this._z__parameter);
        }
    }
}