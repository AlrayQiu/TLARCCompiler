namespace Tlarc.Compiler;

static class PreProcess
{
    public static string Process(string input)
    {
        string ret = "";
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '#')
                while (input[i++] != '\n' && i < input.Length)
                    continue;
            if (input[i] == '\n')
            {
                ret += ' ';
                continue;
            }
            if (i < input.Length)
                ret += input[i];
        }
        return ret;
    }
}