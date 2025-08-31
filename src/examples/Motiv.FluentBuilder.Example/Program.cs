using System.Numerics;
using Motiv.FluentFactory.Generator;

// Check for compile time errors
Rectangle<int>.WithWidth(10).WithHeight(20).Create();
Square<int>.WithWidth(10).Create();
Circle<int>.WithRadius(5).Create();
Cuboid<int>.WithWidth(10).WithHeight(20).WithDepth(30).Create();

Shape<decimal>.WithWidth(10).WithHeight(20).CreateRectangle();
Shape<double>.WithWidth(10).WithHeight(20).CreateDiamond();
Shape<float>.WithWidth(10).WithHeight(20).WithDepth(30).CreateCuboid();
Shape<int>.WithWidth(10).CreateSquare();
Shape<long>.WithRadius(5).CreateCircle();

[FluentFactory]
public partial class Shape<T> where T : INumber<T>;

[FluentFactory]
[FluentConstructor(typeof(Square<>))]
[FluentConstructor(typeof(Shape<>), CreateMethodName = "CreateSquare")]
public partial record Square<T>(T Width) where T : INumber<T>;

[FluentFactory]
[FluentConstructor(typeof(Rectangle<>))]
[FluentConstructor(typeof(Shape<>), CreateMethodName = "CreateRectangle")]
public partial record Rectangle<T>(T Width, T Height) where T : INumber<T>;

[FluentFactory]
[FluentConstructor(typeof(Circle<>))]
[FluentConstructor(typeof(Shape<>), CreateMethodName = "CreateCircle")]
public partial record Circle<T>(T Radius) where T : INumber<T>;

[FluentFactory]
[FluentConstructor(typeof(Diamond<>))]
[FluentConstructor(typeof(Shape<>), CreateMethodName = "CreateDiamond")]
public partial record Diamond<T>(T Width, T Height) where T : INumber<T>;

[FluentFactory]
[FluentConstructor(typeof(Cuboid<>))]
[FluentConstructor(typeof(Shape<>), CreateMethodName = "CreateCuboid")]
public partial record Cuboid<T>(T Width, T Height, T Depth) where T : INumber<T>;
