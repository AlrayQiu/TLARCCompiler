// See https://aka.ms/new-console-template for more information

using Tlarc.Compiler;
using Tlarc.Compiler.SyntaxAnalysis;
Console.ForegroundColor = ConsoleColor.Blue;
Console.BackgroundColor = ConsoleColor.Black;
Console.WriteLine(@"
 _________  ___       ________  ________  ________     
|\___   ___\\  \     |\   __  \|\   __  \|\   ____\    
\|___ \  \_\ \  \    \ \  \|\  \ \  \|\  \ \  \___|    
     \ \  \ \ \  \    \ \   __  \ \   _  _\ \  \       
      \ \  \ \ \  \____\ \  \ \  \ \  \\  \\ \  \____  
       \ \__\ \ \_______\ \__\ \__\ \__\\ _\\ \_______\
        \|__|  \|_______|\|__|\|__|\|__|\|__|\|_______|");
Console.BackgroundColor = ConsoleColor.DarkBlue;
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\nAlray'S Top level robot control system");

Console.BackgroundColor = ConsoleColor.Black;
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine(@"
 ____  ____  _      ____  _  _     _____ ____ 
/   _\/  _ \/ \__/|/  __\/ \/ \   /  __//  __\
|  /  | / \|| |\/|||  \/|| || |   |  \  |  \/|
|  \__| \_/|| |  |||  __/| || |_/\|  /_ |    /
\____/\____/\_/  \|\_/   \_/\____/\____\\_/\_\
                                                                                                                                                                      
");

Console.ForegroundColor = ConsoleColor.White;
LexicalAnalizer lexicalAnalizer = new();
lexicalAnalizer.SetDescriptionDirectory("./descriptions/")
            .SetDescriptionFile("Lexv1.yaml")
            .LoadDescription();

var srcData = lexicalAnalizer.Process(File.ReadAllText("./test/a.tkd"));
srcData.Echo();

var lalr = LALR1Analyser.Generate(
    new Syntax("tlarc_conf",
        // Program -> Processes
        new("Program",
            NonTerminalSymbol.Get("Processes"),
            Syntax.EndOfInputSymbol),
        // Processes -> (Process)* Processes
        new("Processes",
            NonTerminalSymbol.Get("Process"),
            NonTerminalSymbol.Get("Processes")),
        new("Processes",
            NonTerminalSymbol.Get("Process"),
            Syntax.EndOfInputSymbol),
        // Process -> ProcessDefine ProcessField
        new("Process",
            NonTerminalSymbol.Get("ProcessDefine"),
            NonTerminalSymbol.Get("ProcessField")),
        // ProcessDefine -> process Identiider
        new("ProcessDefine",
            TerminalSymbol.Get("Keyword", "process"),
            TerminalSymbol.Get("Identifier")),
        // ProcessField -> { Threads }
        new("ProcessField",
            TerminalSymbol.Get("Delimiters", "{"),
            NonTerminalSymbol.Get("Threads"),
            TerminalSymbol.Get("Delimiters", "}")),
        // Threads -> Thread Threads
        new("Threads",
            NonTerminalSymbol.Get("Thread"),
            NonTerminalSymbol.Get("Threads")),
        new("Threads",
            NonTerminalSymbol.Get("Thread")),
        // Thread -> ThreadDefine ThreadField
        new("Thread",
            NonTerminalSymbol.Get("ThreadDefine"),
            NonTerminalSymbol.Get("ThreadField")),
        // ProcessDefine -> process Identiider
        new("ThreadDefine",
            TerminalSymbol.Get("Keyword", "thread"),
            TerminalSymbol.Get("Identifier")),
        // ThreadField -> { Assgins }
        new("ThreadField",
            TerminalSymbol.Get("Delimiters", "{"),
            NonTerminalSymbol.Get("Assgins"),
            TerminalSymbol.Get("Delimiters", "}")),
        // Assgins -> Assign Assgins
        new("Assgins",
            NonTerminalSymbol.Get("Assgin"),
            NonTerminalSymbol.Get("Assgins")),
        new("Assgins",
            NonTerminalSymbol.Get("Assgin")),
        new("Assgins",
            Syntax.EpsilonSymbol),
        // Assgin -> Identifier = Int;
        new("Assgin",
            TerminalSymbol.Get("Identifier"),
            NonTerminalSymbol.Get("Equal"),
            TerminalSymbol.Get("Int")),
        // Assgin -> Identifier = Float;
        new("Assgin",
            TerminalSymbol.Get("Identifier"),
            NonTerminalSymbol.Get("Equal"),
            TerminalSymbol.Get("Float"))
    ).SetStartSymbol(NonTerminalSymbol.Get("Program"))
);


var actionTable = lalr.ActionTable;
var gotoTable = lalr.GotoTable;
Console.Write($"\n{LALR1Analyser.PrintParsingTables(actionTable, gotoTable)}");