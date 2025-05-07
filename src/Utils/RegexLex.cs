using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tlarc.Compiler.Regex;

internal class RegexParser(string filePath)
{
    private string _inputAll = File.ReadAllText(filePath);
    private string _input = "";
    private int _position = 0;

    public Dictionary<string, DFA> Parse()
    {
        Dictionary<string, DFA> ret = [];
        var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(NullNamingConvention.Instance)
                        .Build();
        var pairs = deserializer.Deserialize<Dictionary<string, string>>(_inputAll);
        foreach (var pair in pairs)
        {
            _position = 0;
            _input = pair.Value;
            ret[pair.Key] = NfaToDfaConvertor.Convert(ParseExpression().Build());
        }
        return ret;
    }
    IASTNode ParseExpression()
    {
        var term = ParseTerm();
        while (Peek() == '|')
        {
            Advance();
            var next = ParseTerm();
            term = new AlternateNode(term, next);
        }
        return term;
    }
    private IASTNode ParseTerm()
    {
        IASTNode node = ParseFactor();
        while (Peek() != null && Peek() != ')' && Peek() != '|')
        {
            var next = ParseFactor();
            node = new ConcatNode(node, next);
        }
        return node;
    }


    protected char? Peek() => _position < _input.Length ? _input[_position] : null;
    protected void Advance() => _position++;

    private IASTNode ParseFactor()
    {
        var node = ParseFactorInner();
        if (Peek() == '*')
        {
            Advance();
            return new ClosureNode(node);
        }

        return node;
    }
    private IASTNode ParseFactorInner()
    {
        switch (Peek())
        {
            case '(':
                Advance();
                if (Peek() == ')')
                {
                    Advance();
                    return new EplisionNode();
                }
                var expr = ParseExpression();
                if (Peek() != ')') throw new KeyNotFoundException("Missing closing parenthesis");
                Advance();
                return expr;
            default:
                var p = Peek();
                Advance();
                return new CharacterNode(p ?? throw new NotSupportedException("Chould not found end of this file, Please check the file is completed"));
        }
    }

}

class RegexLex
{
    Dictionary<string, DFA> dfas { get; } = [];
    public RegexLex(string filePath)
    {
        dfas = new RegexParser(filePath).Parse();
    }

    public RegexLex() { }

    public void Test()
    {
        Console.WriteLine("============================================================\n\t\t\tDFA 状态转移表\n============================================================");
        foreach (var pair in dfas)
        {
            Console.WriteLine($"{pair.Key}:");
            foreach (var node in pair.Value.Nodes.OrderBy(x => x.ID))
            {
                Console.Write($"{(node.ID == pair.Value.Start.ID ? "  =>" : "")}\tState {node.ID}{(node.ISAcceptable ? "接受" : "  ")}");
                foreach (var trans in node.Transition.OrderBy(x => x.Key))
                    Console.Write($"\t{trans.Key} -> {trans.Value.ID}");
                Console.WriteLine();
            }
            Console.WriteLine($"===============================");
        }
    }

    public bool TryMatch(in string srcString, out string type)
    {
        type = "";
        foreach (var dfa in dfas)
        {
            var current = dfa.Value.Start;
            bool isbreak = false;
            foreach (var c in srcString)
            {
                if (!current.Transition.TryGetValue(c, out var nextNode))
                {
                    isbreak = true;
                    break;
                }

                current = nextNode;
            }
            if (current.ISAcceptable && !isbreak)
            {
                type = dfa.Key;
                return true;
            }
        }
        return false;
    }

}