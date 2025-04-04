// See https://aka.ms/new-console-template for more information

using Tlarc.Compiler;
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

// var srcData = lexicalAnalizer.Process(File.ReadAllText("../../../test/a.tkd"));
// srcData.Echo();