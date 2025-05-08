using System.Text;

namespace Tlarc.Compiler.SyntaxAnalysis;


class LALR1Analyser
{
    public static string PrintParsingTables(Dictionary<int, Dictionary<Symbol, ParseAction>> actionTable,
     Dictionary<int, Dictionary<Symbol, int>> gotoTable)
    {
        var sb = new StringBuilder();

        // 打印 Action 表
        sb.AppendLine("LALR(1) Action Table:");
        sb.AppendLine("---------------------------------------------------");
        sb.AppendLine("State | Symbol         | Action");
        sb.AppendLine("---------------------------------------------------");

        foreach (var state in actionTable.Keys.OrderBy(k => k))
        {
            var actions = actionTable[state];
            foreach (var symbol in actions.Keys.OrderBy(s => s.Name))
            {
                var action = actions[symbol];
                sb.AppendLine($"{state,-5} | {FormatSymbol(symbol),-14} | {FormatAction(action)}");
            }
        }

        // 打印 Goto 表
        sb.AppendLine("\nLALR(1) Goto Table:");
        sb.AppendLine("---------------------------------------------------");
        sb.AppendLine("State | Symbol         | Goto State");
        sb.AppendLine("---------------------------------------------------");

        foreach (var state in gotoTable.Keys.OrderBy(k => k))
        {
            var gotos = gotoTable[state];
            foreach (var symbol in gotos.Keys.OrderBy(s => s.Name))
            {
                sb.AppendLine($"{state,-5} | {FormatSymbol(symbol),-14} | {gotos[symbol]}");
            }
        }

        return sb.ToString();
    }

    private static string FormatSymbol(Symbol symbol)
    {
        return symbol.SymbolType == Symbol.Type.Terminal
            ? $"'{symbol.Name}'"
            : symbol.Name;
    }

    private static string FormatAction(ParseAction action)
    {
        return action.Type switch
        {
            ParseActionType.Shift => $"Shift({action.TargetState})",
            ParseActionType.Reduce => $"Reduce({action.ReduceSentence?.ID})",
            ParseActionType.Accept => "Accept",
            _ => "Error"
        };
    }

    public class LrItem(Sentence sentense, int dot, Symbol lookahead)
    {
        public Sentence Sentence { get; } = sentense;
        public int DotPosition { get; } = dot;
        public Symbol Lookahead { get; } = lookahead;

        public bool IsReduceItem => DotPosition >= Sentence.RightSymbols.Count;
        public Symbol? NextSymbol =>
            DotPosition < Sentence.RightSymbols.Count ? Sentence.RightSymbols[DotPosition] : null;
    }


    public class LalrState
    {
        public int Id { get; }
        public HashSet<LrItem> KernelItems { get; } = new HashSet<LrItem>();

        public LalrState(int id)
        {
            Id = id;
        }


    }

    public class ParseAction
    {
        public ParseActionType Type { get; }
        public int TargetState { get; }
        public Sentence? ReduceSentence { get; }

        private ParseAction(ParseActionType type, int state, Sentence? sentence)
        {
            Type = type;
            TargetState = state;
            ReduceSentence = sentence;
        }

        public static ParseAction Shift(int state) =>
            new(ParseActionType.Shift, state, null);
        public static ParseAction Reduce(Sentence sentence) =>
            new(ParseActionType.Reduce, -1, sentence);
        public static ParseAction Accept =>
            new(ParseActionType.Accept, -1, null);
        public static ParseAction Error =>
            new(ParseActionType.Error, -1, null);
    }

    public static Generator Generate(Syntax syntex) => new Generator(syntex).BuildParsingTable();

    public enum ParseActionType { Shift, Reduce, Accept, Error }
    public class Generator(Syntax syntex)
    {
        private readonly Syntax _syntax = syntex;
        private List<LalrState> _states = new List<LalrState>();
        private Dictionary<int, Dictionary<Symbol, ParseAction>> _actionTable = [];
        private Dictionary<int, Dictionary<Symbol, int>> _gotoTable = [];
        public Dictionary<int, Dictionary<Symbol, ParseAction>> ActionTable => _actionTable;
        public Dictionary<int, Dictionary<Symbol, int>> GotoTable => _gotoTable;
        private string GetKernelSignature(IEnumerable<LrItem> kernel)
        {
            return string.Join("|", kernel
                .OrderBy(i => i.Sentence.ID)
                .ThenBy(i => i.DotPosition)
                .Select(i => $"{i.Sentence.ID}:{i.DotPosition}:{i.Lookahead.Name}"));
        }
        private LalrState? FindExistingState(IEnumerable<LrItem> kernel)
        {
            var newKernelSignature = GetKernelSignature(kernel);

            foreach (var state in _states)
            {
                var existingSignature = GetKernelSignature(state.KernelItems);
                if (existingSignature == newKernelSignature)
                {
                    return state;
                }
            }
            return null;
        }

        private string GetStateSignature(LalrState state) => string.Join(";", state.KernelItems
                .OrderBy(i => i.Sentence.ID)
                .ThenBy(i => i.DotPosition)
                .Select(i => $"{i.Sentence.ID},{i.DotPosition},{i.Lookahead.Name}"));

        private void UpdateStateReferences(LalrState oldState, LalrState newState)
        {
            foreach (var stateEntry in _actionTable)
            {
                var actions = stateEntry.Value;
                foreach (var symbol in actions.Keys.ToList())
                {
                    var action = actions[symbol];
                    if (action.Type == ParseActionType.Shift && action.TargetState == oldState.Id)
                    {
                        actions[symbol] = ParseAction.Shift(newState.Id);
                    }
                }
            }

            foreach (var gotoEntry in _gotoTable)
            {
                var gotos = gotoEntry.Value;
                foreach (var symbol in gotos.Keys.ToList())
                {
                    if (gotos[symbol] == oldState.Id)
                    {
                        gotos[symbol] = newState.Id;
                    }
                }
            }
        }
        private void UpdateActionTable(LalrState currentState, Symbol terminal, LalrState targetState)
        {
            if (!_actionTable.TryGetValue(currentState.Id, out Dictionary<Symbol, ParseAction>? value))
            {
                value = [];
                _actionTable[currentState.Id] = value;
            }

            var actions = value;

            if (actions.TryGetValue(terminal, out var existingAction))
            {
                // 冲突检测逻辑（示例：Shift/Reduce冲突）
                if (existingAction.Type == ParseActionType.Shift && existingAction.TargetState != targetState.Id)
                {
                    throw new InvalidOperationException(
                        $"移进-移进冲突：状态 {currentState.Id}，符号 {terminal.Name}，" +
                        $"已有动作 Shift({existingAction.TargetState})，新动作 Shift({targetState.Id})"
                    );
                }
                else if (existingAction.Type == ParseActionType.Reduce)
                {
                    throw new InvalidOperationException(
                        $"移进-规约冲突：状态 {currentState.Id}，符号 {terminal.Name}，" +
                        $"已有动作 Reduce({existingAction.ReduceSentence?.ID})，新动作 Shift({targetState.Id})"
                    );
                }
            }
            else if (targetState.KernelItems.Any(item => item.IsReduceItem && item.Sentence.LeftSymbols.Name == Syntax.StartSymbol?.Name))
                actions[terminal] = ParseAction.Accept;

            else
            {
                actions[terminal] = ParseAction.Shift(targetState.Id);

                var targets = targetState.KernelItems.Where(item => item.IsReduceItem).ToList();
                foreach (var i in targets)
                {
                    if (i.IsReduceItem)
                        actions[i.Lookahead] = ParseAction.Reduce(i.Sentence);
                }
            }

        }
        private void UpdateGotoTable(LalrState currentState, Symbol nonTerminal, LalrState targetState)
        {
            if (!_gotoTable.TryGetValue(currentState.Id, out Dictionary<Symbol, int>? value))
            {
                value = [];
                _gotoTable[currentState.Id] = value;
            }

            var gotos = value;

            // 检查是否已存在该非终结符的转移
            if (gotos.TryGetValue(nonTerminal, out var existingStateId))
            {
                if (existingStateId != targetState.Id)
                {
                    throw new InvalidOperationException(
                        $"Goto冲突：状态 {currentState.Id}，符号 {nonTerminal.Name}，" +
                        $"已有转移 → {existingStateId}，新转移 → {targetState.Id}"
                    );
                }
            }
            else
            {
                // 无冲突，添加转移
                gotos[nonTerminal] = targetState.Id;
            }
        }
        internal Generator BuildParsingTable()
        {
            // 步骤1: 构建初始状态
            var startProduction = _syntax.Sentences.First();
            var initialItem = new LrItem(
                startProduction,
                0,
                Syntax.EndOfInputSymbol
            );
            var initialState = new LalrState(0);
            initialState.KernelItems.Add(initialItem);
            ComputeClosure(initialState);
            _states.Add(initialState);

            // 步骤2: 状态扩展和合并
            var stateQueue = new Queue<LalrState>();
            stateQueue.Enqueue(initialState);

            while (stateQueue.Count > 0)
            {
                var currentState = stateQueue.Dequeue();

                var transitionSymbols = currentState.KernelItems
                    .Select(i => i.NextSymbol)
                    .Where(s => s != null)
                    .DistinctBy(s => s?.Name);

                foreach (var symbol in transitionSymbols)
                {
                    // 计算新状态的核心项
                    var newKernel = currentState.KernelItems
                        .Where(i => i.NextSymbol?.Name == symbol?.Name)
                        .Select(i => new LrItem(
                            i.Sentence,
                            i.DotPosition + 1,
                            i.Lookahead
                        ))
                        .ToList();

                    // 查找或创建新状态
                    var newState = new LalrState(_states.Count);
                    newState.KernelItems.UnionWith(newKernel);
                    ComputeClosure(newState);
                    var existingState = FindExistingState(newState.KernelItems);
                    if (existingState == null)
                    {
                        _states.Add(newState);
                        stateQueue.Enqueue(newState);
                        existingState = newState;
                    }

                    // 更新转移表
                    if (symbol?.SymbolType == Symbol.Type.Terminal)
                    {
                        UpdateActionTable(currentState, symbol, existingState);
                    }
                    else if (symbol?.SymbolType == Symbol.Type.NonTerminal)
                    {
                        UpdateGotoTable(currentState, symbol, existingState);
                    }
                }
            }

            // 步骤3: 合并同心状态
            MergeStates();
            return this;
        }

        private void ComputeClosure(LalrState state)
        {
            var itemsToProcess = new Queue<LrItem>(state.KernelItems);
            var processedItems = new HashSet<LrItem>();

            while (itemsToProcess.Count > 0)
            {
                var item = itemsToProcess.Dequeue();
                if (!processedItems.Add(item)) continue;

                var nextSymbol = item.NextSymbol;
                if (nextSymbol?.SymbolType == Symbol.Type.NonTerminal)
                {
                    foreach (var sentence in _syntax.GetSentence(nextSymbol))
                    {
                        var lookaheads = ComputeLookahead(item);
                        foreach (var la in lookaheads)
                        {
                            var newItem = new LrItem(sentence, 0, la);
                            if (state.KernelItems.Add(newItem))
                            {
                                itemsToProcess.Enqueue(newItem);
                            }
                        }
                    }
                }
            }
        }

        private HashSet<Symbol> ComputeFirst(IEnumerable<Symbol> symbols)
        {
            var firstSet = new HashSet<Symbol>();
            foreach (var symbol in symbols)
            {
                var symbolFirst = GetFirst(symbol);
                firstSet.UnionWith(symbolFirst.Where(s => s.Name != Syntax.EpsilonSymbol.Name));

                if (!symbolFirst.Any(s => s.Name == Syntax.EpsilonSymbol.Name))
                {
                    break;
                }
            }
            if (symbols.All(s => GetFirst(s).Any(f => f.Name == Syntax.EpsilonSymbol.Name)))
            {
                firstSet.Add(Syntax.EpsilonSymbol);
            }

            return firstSet;
        }

        private readonly Dictionary<Symbol, HashSet<Symbol>> _firstCache = [];

        private HashSet<Symbol> GetFirst(Symbol symbol)
        {
            if (_firstCache.TryGetValue(symbol, out var cached))
            {
                return cached;
            }

            var first = new HashSet<Symbol>();
            if (symbol.SymbolType == Symbol.Type.Terminal)
            {
                first.Add(symbol);
                return first;
            }

            foreach (var sent in _syntax.GetSentence(symbol))
            {
                if (sent.RightSymbols.Count == 0)
                {
                    first.Add(Syntax.EpsilonSymbol);
                    continue;
                }

                var prodFirst = ComputeFirst(sent.RightSymbols);
                first.UnionWith(prodFirst);
            }

            _firstCache[symbol] = first;
            return first;
        }

        private IEnumerable<Symbol> ComputeLookahead(LrItem item)
        {
            var beta = item.Sentence.RightSymbols
                .Skip(item.DotPosition + 1)
                .Append(item.Lookahead);

            return ComputeFirst(beta);
        }

        // 合并同心状态
        private void MergeStates()
        {
            var stateGroups = _states
                .GroupBy(s => GetStateSignature(s))
                .ToList();

            foreach (var group in stateGroups.Where(g => g.Count() > 1))
            {
                var mergedState = group.First();
                foreach (var state in group.Skip(1))
                {
                    foreach (var item in state.KernelItems)
                    {
                        mergedState.KernelItems.Add(item);
                    }
                    UpdateStateReferences(state, mergedState);
                }
            }
        }

        // 其他辅助方法（省略具体实现细节）...
    }
}