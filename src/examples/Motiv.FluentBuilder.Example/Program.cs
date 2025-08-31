using Motiv.Generator;

// Check for compile time errors
Rectangle.WithWidth(10).WithHeight(20).Create();
Square.WithWidth(10).Create();
Circle.WithRadius(5).Create();
Cuboid.WithWidth(10).WithHeight(20).WithDepth(30).Create();

Shape.WithWidth(10).WithHeight(20).Create();
Shape.WithWidth(10).Create();
Shape.WithRadius(5).Create();
Shape.WithWidth(10).WithHeight(20).WithDepth(30).Create();

[FluentFactory]
public partial class Shape;

[FluentFactory]
[FluentConstructor(typeof(Square))]
[FluentConstructor(typeof(Shape))]
public partial record Square(int Width);

[FluentFactory]
[FluentConstructor(typeof(Rectangle))]
[FluentConstructor(typeof(Shape))]
public partial record Rectangle(int Width, int Height);

[FluentFactory]
[FluentConstructor(typeof(Circle))]
[FluentConstructor(typeof(Shape))]
public partial record Circle(int Radius);

[FluentFactory]
[FluentConstructor(typeof(Cuboid))]
[FluentConstructor(typeof(Shape))]
public partial record Cuboid(int Width, int Height, int Depth);
