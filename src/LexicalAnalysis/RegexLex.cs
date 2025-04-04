using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tlarc.Compiler.Regex;

internal class NFANode
{
    private static int _nextID = 0;
    public int ID { get; } = _nextID++;
    public Dictionary<char, HashSet<NFANode>> Transtions { get; } = [];
    public HashSet<NFANode> EpsilonClosureTranstions { get; } = [];

    public void AddTransition(char symbol, NFANode node)
    {
        if (!Transtions.ContainsKey(symbol))
            Transtions[symbol] = [];
        Transtions[symbol].Add(node);
    }
    public void AddEpsilonClosureTransition(NFANode node) => EpsilonClosureTranstions.Add(node);
}
internal class NFAConnection(NFANode start, NFANode end)
{
    public NFANode Start => start;
    public NFANode End => end;
}

internal class DFANode
{
    public int ID { get; internal set; }
    public bool ISAcceptable { get; internal set; }
    public Dictionary<char, DFANode> Transition { get; } = [];
}
internal class DFA(DFANode start)
{
    public DFANode Start => start;
    public IEnumerable<DFANode> Nodes
    {
        get
        {
            var visited = new HashSet<DFANode>();
            var queue = new Queue<DFANode>();
            queue.Enqueue(start);
            visited.Add(start);
            while (queue.Count > 0)
            {
                var state = queue.Dequeue();
                yield return state;
                foreach (var next in state.Transition.Values)
                {
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
        }
    }
}

interface IASTNode
{
    public NFAConnection Build();
}

internal class CharacterNode(char c) : IASTNode
{
    public char Character => c;
    public NFAConnection Build()
    {
        var start = new NFANode();
        var end = new NFANode();
        start.AddTransition(Character, end);
        return new(start, end);
    }
}
internal class ConcatNode(IASTNode left, IASTNode right) : IASTNode
{
    public IASTNode Left => left;
    public IASTNode Right => right;

    public NFAConnection Build()
    {
        var leftNFA = left.Build();
        var rightNFA = right.Build();

        leftNFA.End.AddEpsilonClosureTransition(rightNFA.Start);

        return new(leftNFA.Start, rightNFA.End);
    }
}
internal class AlternateNode(IASTNode left, IASTNode right) : IASTNode
{
    IASTNode Left => left;
    IASTNode Right => right;
    public NFAConnection Build()
    {
        var leftNFA = left.Build();
        var rightNFA = right.Build();

        var start = new NFANode();
        start.AddEpsilonClosureTransition(leftNFA.Start);
        start.AddEpsilonClosureTransition(rightNFA.Start);

        var end = new NFANode();
        leftNFA.End.AddEpsilonClosureTransition(end);
        rightNFA.End.AddEpsilonClosureTransition(end);

        return new(start, end);
    }
}
internal class ClosureNode(IASTNode innerNode) : IASTNode
{
    public IASTNode InnerNode => innerNode;
    public NFAConnection Build()
    {
        var innerNfa = innerNode.Build();
        var start = new NFANode();
        var end = new NFANode();

        start.AddEpsilonClosureTransition(innerNfa.Start);
        start.AddEpsilonClosureTransition(end);


        innerNfa.End.AddEpsilonClosureTransition(innerNfa.Start);
        innerNfa.End.AddEpsilonClosureTransition(end);

        return new(start, end);
    }
}

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

internal static class NfaToDfaConvertor
{
    public static DFA Convert(NFAConnection nfa)
    {
        var nfaEnd = nfa.End;
        Dictionary<HashSet<NFANode>, DFANode> dfaNodes = new(new NFASetComparer());
        var initNodeSet = EpsilonClosure([nfa.Start]);
        var initState = dfaNodes[initNodeSet] = new() { ID = 0, ISAcceptable = initNodeSet.Contains(nfaEnd) };

        Queue<HashSet<NFANode>> queue = [];
        queue.Enqueue(initNodeSet);

        var alphabet = Alphabet(nfa);
        int nextId = 1;

        while (queue.Count > 0)
        {
            var currentStateSet = queue.Dequeue();
            var currentDfaStateSet = dfaNodes[currentStateSet];

            foreach (var symbol in alphabet)
            {
                var moved = Move(currentStateSet, symbol);
                var newStateSet = EpsilonClosure(moved);

                if (newStateSet.Count == 0) continue;

                if (!dfaNodes.TryGetValue(newStateSet, out var newDfaState))
                {
                    newDfaState = new()
                    {
                        ID = nextId++,
                        ISAcceptable = newStateSet.Contains(nfaEnd)
                    };
                    dfaNodes[newStateSet] = newDfaState;
                    queue.Enqueue(newStateSet);
                }
                currentDfaStateSet.Transition[symbol] = newDfaState;
            }
        }

        return new(initState);
    }

    private static HashSet<NFANode> Move(IEnumerable<NFANode> nodes, char symbol)
    {
        var rst = new HashSet<NFANode>();
        foreach (var node in nodes)
            if (node.Transtions.TryGetValue(symbol, out var nextNode))
                rst.UnionWith(nextNode);
        return rst;
    }

    private static HashSet<NFANode> EpsilonClosure(IEnumerable<NFANode> nodes)
    {
        HashSet<NFANode> closure = [];
        Stack<NFANode> stack = new(nodes);
        while (stack.Count > 0)
        {
            var state = stack.Pop();
            if (!closure.Add(state))
                continue;
            foreach (var eps in state.EpsilonClosureTranstions)
                stack.Push(eps);
        }
        return closure;
    }

    private static HashSet<char> Alphabet(NFAConnection nfa)
    {
        HashSet<char> symbols = [];
        HashSet<NFANode> visited = [];
        Stack<NFANode> stack = [];
        stack.Push(nfa.Start);

        while (stack.Count > 0)
        {
            var state = stack.Pop();
            if (!visited.Add(state)) continue;

            foreach (var symbol in state.Transtions.Keys)
                symbols.Add(symbol);
            foreach (var eps in state.EpsilonClosureTranstions)
                if (!visited.Contains(eps))
                    stack.Push(eps);
            foreach (var nodes1 in state.Transtions.Values)
                foreach (var node in nodes1)
                    if (!visited.Contains(node))
                        stack.Push(node);
        }

        return symbols;
    }

    private class NFASetComparer : IEqualityComparer<HashSet<NFANode>>
    {
        public bool Equals(HashSet<NFANode>? x, HashSet<NFANode>? y)
        {
            if (x?.Count != y?.Count) return false;
            if (x == null || y == null)
                throw new NullReferenceException();
            return x.All(y.Contains) && y.All(x.Contains);
        }

        public int GetHashCode([DisallowNull] HashSet<NFANode> obj) => obj.GetHashCode();
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
                Console.Write($"State {node.ID}{(node.ISAcceptable ? "接受" : "  ")}");
                foreach (var trans in node.Transition.OrderBy(x => x.Key))
                    Console.Write($"\t{trans.Key} -> {trans.Value.ID}");
                Console.WriteLine();
            }
            Console.WriteLine($"===============================");
        }
    }

}