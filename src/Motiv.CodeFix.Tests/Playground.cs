namespace Motiv.CodeFix.Tests;

public class Playground
{
    [Theory]
    [InlineData(0, -1, 5)]
    public void Test(int valueA, int valueB, int valueC)
    {
        var inRange = valueA >= 0 && (valueB >= 0 || valueC >= 1);
    }
}
