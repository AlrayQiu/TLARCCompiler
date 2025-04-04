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
        throw new NotImplementedException();
    }
    string _desciptionDirctory = "";
    string _desciptionFile = "";
    internal string DescriptionPath => $"{_desciptionDirctory}{_desciptionFile}";
    private RegexLex _regexLex = new();
}