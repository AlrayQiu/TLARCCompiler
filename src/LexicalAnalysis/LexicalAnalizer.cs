using System.Text.RegularExpressions;
using Tlarc.Compiler.LexicalAnalysis;
using Tlarc.Compiler.Regex;

namespace Tlarc.Compiler;

partial class LexicalAnalizer()
{
    public LexicalAnalizer SetDescriptionDirectory(string path)
    {
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException(path);
        if (_desciptionDirctory.EndsWith('/'))
            _desciptionDirctory = path;
        else
            _desciptionDirctory = $"{path}/";

        return this;
    }
    public LexicalAnalizer SetDescriptionFile(string descriptionFile)
    {
        _desciptionFile = descriptionFile;
        if (!File.Exists(DescriptionPath))
            throw new FileNotFoundException(DescriptionPath);
        return this;
    }
    public LexicalAnalizer LoadDescription()
    {
        _regexLex = new(DescriptionPath);
        _regexLex.Test();
        return this;
    }
    public LexTable Process(string src)
    {
        LexTable lexTable = new();
        var strs = PreProcess.Process(src).Split(' ', options: StringSplitOptions.RemoveEmptyEntries);
        foreach (var str in strs)
        {
            if (_regexLex.TryMatch(str, out var type))
                lexTable.StepNextResult(type, str);
            else throw new NotSupportedException($"Unknow word : {str}");
        }
        return lexTable;
    }
    string _desciptionDirctory = "";
    string _desciptionFile = "";
    internal string DescriptionPath => $"{_desciptionDirctory}{_desciptionFile}";
    private RegexLex _regexLex = new();
}