namespace Motiv.CodeFix.Tests;

public class Playground
{
    public bool IsFeatureEnabled(int valueA, int valueB, int valueC)
    {
        return (valueA >= 0 && 1 < valueC) || (valueB >= 0 && 1 < valueC);
    }
}
