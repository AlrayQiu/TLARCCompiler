using System.Diagnostics.CodeAnalysis;

namespace Tlarc.Compiler;

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
