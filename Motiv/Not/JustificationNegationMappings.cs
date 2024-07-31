using System.Collections.ObjectModel;

namespace Motiv.Not;

internal class JustificationNegationMappings() : ReadOnlyDictionary<string, string>(
    new Dictionary<string, string>
    {
        ["NAND"] = "AND",
        ["NOR"] = "OR",
        ["XNOR"] = "XOR",
        ["AND"] = "NAND",
        ["OR"] = "NOR",
        ["XOR"] = "XNOR"
    })
{
    public static JustificationNegationMappings Instance { get; } = new();
}
