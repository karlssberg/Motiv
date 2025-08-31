using System;

namespace Motiv.FluentBuilder.Example
{
    public partial record Rectangle
    {
        /// <summary>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Rectangle"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static Step_0__Motiv_FluentBuilder_Example_Rectangle WithWidth(in int width)
        {
            return new Step_0__Motiv_FluentBuilder_Example_Rectangle(width);
        }
    }

    /// <summary>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Rectangle"/>
    /// </summary>
    public struct Step_0__Motiv_FluentBuilder_Example_Rectangle
    {
        private readonly int _width__parameter;
        internal Step_0__Motiv_FluentBuilder_Example_Rectangle(in int width)
        {
            this._width__parameter = width;
        }

        /// <summary>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Rectangle"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_1__Motiv_FluentBuilder_Example_Rectangle WithHeight(in int height)
        {
            return new Step_1__Motiv_FluentBuilder_Example_Rectangle(this._width__parameter, height);
        }
    }

    /// <summary>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Rectangle"/>
    /// </summary>
    public struct Step_1__Motiv_FluentBuilder_Example_Rectangle
    {
        private readonly int _width__parameter;
        private readonly int _height__parameter;
        internal Step_1__Motiv_FluentBuilder_Example_Rectangle(in int width, in int height)
        {
            this._width__parameter = width;
            this._height__parameter = height;
        }

        /// <summary>
        /// Creates a new instance using constructor Motiv.FluentBuilder.Example.Rectangle.Rectangle(int Width, int Height).
        ///
        ///     <seealso cref="Motiv.FluentBuilder.Example.Rectangle"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Rectangle Create()
        {
            return new Rectangle(this._width__parameter, this._height__parameter);
        }
    }
}