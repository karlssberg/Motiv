using System;

namespace Motiv.FluentBuilder.Example
{
    public partial record Circle
    {
        /// <summary>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Circle"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static Step_0__Motiv_FluentBuilder_Example_Circle WithRadius(in int radius)
        {
            return new Step_0__Motiv_FluentBuilder_Example_Circle(radius);
        }
    }

    /// <summary>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Circle"/>
    /// </summary>
    public struct Step_0__Motiv_FluentBuilder_Example_Circle
    {
        private readonly int _radius__parameter;
        internal Step_0__Motiv_FluentBuilder_Example_Circle(in int radius)
        {
            this._radius__parameter = radius;
        }

        /// <summary>
        /// Creates a new instance using constructor Motiv.FluentBuilder.Example.Circle.Circle(int Radius).
        ///
        ///     <seealso cref="Motiv.FluentBuilder.Example.Circle"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Circle Create()
        {
            return new Circle(this._radius__parameter);
        }
    }
}