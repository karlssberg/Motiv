
using Motiv;

namespace MyNamespace
{
    public class Playground()
    {
        public bool IsFeatureEnabled(int valueA, int valueB, int valueC, string text) =>
            (valueA >= 0 && 1 < valueC) || IsGreen(text);

        public static bool IsGreen(string text)
        {
            return text == "green";
        }
    }
}
