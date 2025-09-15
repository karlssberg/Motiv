using Motiv.FluentBuilder.Example;

// Examples of consuming the generated code

Rectangle<int>.WithWidth(10).WithHeight(20).Create();
Square<decimal>.WithWidth(10).Create();
Circle<double>.WithRadius(5).Create();
Cuboid<long>.WithWidth(10).WithHeight(20).WithDepth(30).Create();

Shape<int>.WithWidth(10).CreateSquare();
Shape<decimal>.WithWidth(10).WithHeight(20).CreateRectangle();
Shape<double>.WithWidth(10).WithHeight(20).CreateDiamond();
Shape<float>.WithWidth(10).WithHeight(20).WithDepth(30).CreateCuboid();
Shape<long>.WithRadius(5).CreateCircle();

Shape.WithWidth(10).CreateSquare();
Shape.WithWidth(10m).WithHeight(20m).CreateRectangle();
Shape.WithWidth(10d).WithHeight(20d).CreateDiamond();
Shape.WithWidth(10f).WithHeight(20f).WithDepth(30f).CreateCuboid();
Shape.WithRadius(5L).CreateCircle();

Line.X(10).Y(20).Create();
Line.X(10d).Y(20d).Z(30d).Create();
