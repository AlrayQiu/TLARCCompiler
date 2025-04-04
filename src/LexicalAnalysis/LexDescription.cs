using System.ComponentModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tlarc.Compiler.LexicalAnalysis;

enum LexCategory
{
    Keyword = 1,
    Identifier,
    Int,
    Float,
    String,
    CalculatorSign,
    Delimiters
}
class LexTable
{
    public Dictionary<LexCategory, List<string>> Words { get; internal set; } = new(){
        {LexCategory.Keyword,[]},
        {LexCategory.Int,[]},
        {LexCategory.Float,[]},
        {LexCategory.String,[]},
        {LexCategory.Identifier,[]},
        {LexCategory.Delimiters,[]},
        {LexCategory.CalculatorSign,[]},
    };
    public List<(LexCategory, int)> Rst { get; internal set; } = [];
    public List<string> this[LexCategory category] => Words[category];
    public void StepNextResult(LexCategory category, string data)
    {
        var index = Words[category].IndexOf(data);
        if (index == -1)
        {
            Words[category].Add(data);
            index = Words[category].Count - 1;
        }
        Rst.Add((category, index));
    }

    public void Echo()
    {
        foreach (var i in Rst)
            Console.WriteLine($"<{i.Item1.ToString()},\t {i.Item2},\t {Words[i.Item1][i.Item2]}>");
    }
}