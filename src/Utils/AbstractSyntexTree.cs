

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
internal class CharacterNode(string c) : IASTNode
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

