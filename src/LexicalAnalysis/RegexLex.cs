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
    public Dictionary<char, DFANode> Transition { get; internal set; } = [];
    public static readonly DFANodeComparer Comparer = new();

    public class DFANodeComparer : IEqualityComparer<DFANode>
    {
        public bool Equals(DFANode? x, DFANode? y)
        {
            if (x == null || y == null)
                return false;
            return x.ISAcceptable == y.ISAcceptable && x.Transition.Keys.All(y.Transition.Keys.Contains) && y.Transition.Keys.All(x.Transition.Keys.Contains) && x.Transition.Values.All(y.Transition.Values.Contains) && y.Transition.Values.All(x.Transition.Values.Contains);
        }

        public int GetHashCode([DisallowNull] DFANode obj)
        {
            int hashcode = 17;
            foreach (var i in obj.Transition)
                hashcode = hashcode * 31 + i.Key + i.Value.ID;
            return hashcode;
        }
    }


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
                    if (visited.Contains(next))
                        continue;
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

internal class EplisionNode() : IASTNode
{
    public NFAConnection Build()
    {
        var start = new NFANode();
        var end = new NFANode();
        start.AddEpsilonClosureTransition(end);
        return new(start, end);
    }
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

        return Minimal(new(initState));
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


    private static HashSet<char> Alphabet(DFA dfa)
    {
        var alphabet = new HashSet<char>();
        foreach (var i in dfa.Nodes)
            foreach (var j in i.Transition.Keys)
                alphabet.Add(j);

        return alphabet;
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

        public int GetHashCode([DisallowNull] HashSet<NFANode> obj)
        {
            int hash = 17;
            foreach (var node in obj.OrderBy(x => x.ID))
                hash = hash * 31 + node.ID;
            return hash;
        }
    }

    private static DFA Minimal(in DFA dfa)
    {
        var partitions = new List<HashSet<DFANode>>();
        var accepting = new HashSet<DFANode>();
        var noAccepting = new HashSet<DFANode>();

        foreach (var node in dfa.Nodes)
            if (node.ISAcceptable)
                accepting.Add(node);
            else
                noAccepting.Add(node);

        if (accepting.Count > 0) partitions.Add(accepting);
        if (noAccepting.Count > 0) partitions.Add(noAccepting);

        var partitionQueue = new Queue<HashSet<DFANode>>(partitions);
        var alphabet = Alphabet(dfa);
        while (partitionQueue.Count > 0)
        {
            var current = partitionQueue.Dequeue();
            if (current.Count <= 1)
                continue;

            foreach (var symbol in alphabet)
            {
                var split = SplitPrtition(current, symbol, partitions);
                if (split != null)
                {
                    partitions.Remove(current);
                    partitions.Add(split.Value.p1);
                    partitions.Add(split.Value.p2);

                    partitionQueue.Enqueue(split.Value.p1);
                    partitionQueue.Enqueue(split.Value.p2);
                    break;
                }
            }

        }
        return BuildMinimizedDFA(dfa, partitions);
    }

    private static DFA BuildMinimizedDFA(DFA dfa, List<HashSet<DFANode>> partitions)
    {
        var nodeMapping = new Dictionary<DFANode, DFANode>();
        var newNodes = new List<DFANode>();
        int idCounter = 0;


        foreach (var partition in partitions)
        {
            var newNode = new DFANode()
            {
                ID = idCounter++,
                ISAcceptable = partition.Any(x => x.ISAcceptable)
            };

            foreach (var node in partition)
                nodeMapping[node] = newNode;

            newNodes.Add(newNode);
        }

        foreach (var newNode in newNodes)
        {
            var originNode = partitions.First(x => nodeMapping[x.First()] == newNode).First();

            foreach (var trans in originNode.Transition)
                if (nodeMapping.TryGetValue(trans.Value, out var targetNewNode))
                    newNode.Transition[trans.Key] = targetNewNode;
        }

        var newDFA = new DFA(nodeMapping[dfa.Start]);
        do
        {
            Dictionary<DFANode, DFANode> dict = new(comparer: DFANode.Comparer);
            Dictionary<DFANode, DFANode> dict2 = [];

            var nodeList = newDFA.Nodes.ToList();
            foreach (var nodes in nodeList)
            {

                if (dict.TryGetValue(nodes, out var v1))
                    dict[nodes] = v1;
                else dict[nodes] = nodes;
                dict2.Add(nodes, dict[nodes]);
            }
            foreach (var node in nodeList)
                foreach (var i in node.Transition)
                    node.Transition[i.Key] = dict2[i.Value];

            if (nodeList.All(newDFA.Nodes.ToList().Contains))
                break;
        } while (true);

        return newDFA;
    }

    private static (HashSet<DFANode> p1, HashSet<DFANode> p2)? SplitPrtition(in HashSet<DFANode> partition, char symbol, List<HashSet<DFANode>> all)
    {
        var group = new Dictionary<HashSet<DFANode>, List<DFANode>>();

        foreach (var node in partition)
        {
            if (!node.Transition.TryGetValue(symbol, out var targetNode))
                targetNode = null;

            var targetPartition = all.FirstOrDefault(x => targetNode != null && x.Contains(targetNode));

            bool found = false;
            foreach (var key in group.Keys)
                if (key == targetPartition)
                {
                    group[key].Add(node);
                    found = true;
                    break;
                }

            if (!found)
                group[targetPartition ?? []] = [node];

        }

        if (group.Count > 1)
        {
            var orderedGroups = group.Values.OrderBy(x => -x.Count).ToList();
            var k = new HashSet<DFANode>(partition);
            k.ExceptWith(orderedGroups[0]);
            return ([.. orderedGroups[0]], k);
        }
        return null;
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