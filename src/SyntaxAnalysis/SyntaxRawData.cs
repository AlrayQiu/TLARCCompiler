namespace Tlarc.Compiler.SyntaxAnalysis;

abstract class Symbol
{
    public required string Name { get; init; }
    public enum Type { Terminal, NonTerminal }
    public required Type SymbolType { get; init; }
};




class NonTerminalSymbol : Symbol
{

    public static NonTerminalSymbol Get(string name)
    {
        return new NonTerminalSymbol() { Name = name, SymbolType = Type.NonTerminal };
    }
}
class TerminalSymbol : Symbol
{
    public readonly static Dictionary<(string type, string data), TerminalSymbol> StaticTable = [];
    const string Any = "";
    public required string DataType { get; init; }
    public required string Data { get; init; }


    public static TerminalSymbol Get(string type, string data = TerminalSymbol.Any)
    {
        if (StaticTable.TryGetValue((type, data), out var value))
            return value;
        var newValue = new TerminalSymbol() { Data = data, DataType = type, SymbolType = Type.Terminal, Name = type + data };
        StaticTable[(type, data)] = newValue;
        return newValue;
    }
}


// non_terminal_symbol_left -> symbolsRight(ex:a + b)
class Sentence(string nonTerminalSymbolLeftName, params Symbol[] symbolsRight)
{
    static int StaticID = 0;
    public int ID { get; } = StaticID++;
    public readonly NonTerminalSymbol LeftSymbols = NonTerminalSymbol.Get(nonTerminalSymbolLeftName);
    public readonly List<Symbol> RightSymbols = [.. symbolsRight];
}

class Syntax(string name, params Sentence[] sentences)
{

    private readonly Dictionary<Symbol, List<Sentence>> _productionsByLeft = [];
    public Syntax SetStartSymbol(NonTerminalSymbol symbol)
    {
        StartSymbol = symbol;
        return this;
    }

    public Syntax AddSentence(Sentence production)
    {
        if (!_productionsByLeft.ContainsKey(production.LeftSymbols))
        {
            _productionsByLeft[production.LeftSymbols] = [];
        }
        _productionsByLeft[production.LeftSymbols].Add(production);
        return this;
    }

    public IEnumerable<Sentence> GetSentence(Symbol leftSymbol) =>
            _productionsByLeft.TryGetValue(leftSymbol, out var prods)
            ? prods
            : Enumerable.Empty<Sentence>();

    public string Name { get; init; } = name;

    public static Symbol EndOfInputSymbol => TerminalSymbol.Get("EOI");
    public static Symbol EpsilonSymbol => TerminalSymbol.Get("Epsilon");
    public static Symbol? StartSymbol { get; private set; }
    public required List<Sentence> Sentences { get; init; } = [.. sentences];

}