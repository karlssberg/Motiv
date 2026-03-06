
using Motiv;

public class Playground()
{
    public bool IsFeatureEnabled(int valueA, int valueB, int valueC, string text)
    {
        return (valueA >= 0 && 1 < valueC) || valueB >= 0 && 1 < valueC && (string.IsNullOrEmpty(text) && IsGreen(text));

    }

    public bool IsGreen(string text)
    {
        return text == "green";
    }
}
|
