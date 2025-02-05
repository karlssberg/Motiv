using System;
            using Motiv.Generator.Attributes;

            namespace Test.Namespace
            {
                [FluentFactory]
                public static partial class Factory;

                public class MyBuildTarget<T1, T2>
                {
                    [FluentConstructor(typeof(Factory), Options = FluentOptions.NoCreateMethod)]
                    public MyBuildTarget(
                        [MultipleFluentMethods(typeof(FirstMethods))]T1 first,
                        [MultipleFluentMethods(typeof(SecondMethods))]T2 second)
                    {
                        First = first;
                        Second = second;
                    }

                    public T1 First { get; set; }
                    public T2 Second { get; set; }
                }

                public class FirstMethods
                {
                    [FluentMethodTemplate]
                    public static TX SetFirst<TX>(in Func<TX> function)
                    {
                        return function();
                    }

                    [FluentMethodTemplate]
                    public static int SetFirst(in Func<int> function)
                    {
                        return function();
                    }

                    [FluentMethodTemplate]
                    public static Func<TX2, TX1> SetFirst<TX1, TX2>()
                    {
                        return _ => default(TX1);
                    }
                }

                public class SecondMethods
                {
                    [FluentMethodTemplate]
                    public static TY SetSecond<TY>(in Func<TY> function)
                    {
                        return function();
                    }

                    [FluentMethodTemplate]
                    public static int SetSecond(in Func<int> function)
                    {
                        return function();
                    }

                    [FluentMethodTemplate]
                    public static Func<TY2, TY1> SetSecond<TY1, TY2>()
                    {
                        return _ => default(TY1);
                    }
                }
            }

            namespace Test.Namespace
            {
                public static partial class Factory
                {
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<T1> SetFirst<T1>(in System.Func<T1> function)
                    {
                        return new Step_0__Test_Namespace_Factory<T1>(FirstMethods.SetFirst<T1>(function));
                    }

                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<int> SetFirst(in System.Func<int> function)
                    {
                        return new Step_0__Test_Namespace_Factory<int>(FirstMethods.SetFirst(function));
                    }

                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public static Step_0__Test_Namespace_Factory<System.Func<TX2, TX1>> SetFirst<TX1, TX2>()
                    {
                        return new Step_0__Test_Namespace_Factory<System.Func<TX2, TX1>>(FirstMethods.SetFirst<TX1, TX2>());
                    }
                }

                public struct Step_0__Test_Namespace_Factory<T1>
                {
                    private readonly T1 _first__parameter;
                    public Step_0__Test_Namespace_Factory(in T1 first)
                    {
                        this._first__parameter = first;
                    }

                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, T2> SetSecond<T2>(in System.Func<T2> function)
                    {
                        return new MyBuildTarget<T1, T2>(this._first__parameter, SecondMethods.SetSecond<T2>(function));
                    }

                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, int> SetSecond(in System.Func<int> function)
                    {
                        return new MyBuildTarget<T1, int>(this._first__parameter, SecondMethods.SetSecond(function));
                    }

                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    public MyBuildTarget<T1, System.Func<TY2, TY1>> SetSecond<TY1, TY2>()
                    {
                        return new MyBuildTarget<T1, System.Func<TY2, TY1>>(this._first__parameter, SecondMethods.SetSecond<TY1, TY2>());
                    }
                }
            }
