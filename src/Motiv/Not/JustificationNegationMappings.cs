using System.Collections.ObjectModel;

namespace Motiv.Not;

internal class JustificationNegationMappings() : ReadOnlyDictionary<string, string>(
    new Dictionary<string, string>
    {
        ["NAND"] = "AND",
        ["NAND ALSO"] = "NAND ALSO",
        ["NOR"] = "OR",
        ["NOR ELSE"] = "OR ELSE",
        ["XNOR"] = "XOR",
        ["AND"] = "NAND",
        ["AND ALSO"] = "NAND ALSO",
        ["OR"] = "NOR",
        ["OR ELSE"] = "NOR ELSE",
        ["XOR"] = "XNOR"
    })
{
    public static JustificationNegationMappings Instance { get; } = new();
}
