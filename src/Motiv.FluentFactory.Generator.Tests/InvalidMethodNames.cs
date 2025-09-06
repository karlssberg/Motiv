using System.Collections;

namespace Motiv.FluentFactory.Generator.Tests;

public class InvalidMethodNames : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        // Single character invalids
        yield return [""];
        yield return [" "];
        yield return ["!"];
        yield return ["@"];
        yield return ["#"];
        yield return ["$"];
        yield return ["%"];
        yield return ["^"];
        yield return ["&"];
        yield return ["*"];
        yield return ["("];
        yield return [")"];
        yield return ["-"];
        yield return ["+"];
        yield return ["="];
        yield return ["["];
        yield return ["]"];
        yield return ["{"];
        yield return ["}"];
        yield return ["|"];
        yield return [":"];
        yield return [";"];
        yield return ["'"];
        yield return ["<"];
        yield return [">"];
        yield return [","];
        yield return ["."];
        yield return ["?"];
        yield return ["/"];
        yield return ["~"];
        yield return ["`"];
        // Numbers at the start (these would be invalid as first character)
        yield return ["0"];
        yield return ["1"];
        yield return ["2"];
        yield return ["3"];
        yield return ["4"];
        yield return ["5"];
        yield return ["6"];
        yield return ["7"];
        yield return ["8"];
        yield return ["9"];

        // Additional invalid characters
        yield return ['\0']; // Null character
        yield return ['\b']; // Backspace
        yield return ['\f']; // Form feed
        yield return ['\v']; // Vertical tab
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
