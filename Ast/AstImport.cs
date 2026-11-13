using System;
using BifyLang.Lexer;

namespace BifyLang.Ast
{
    public class AstImport(Token token, AstString pathNode) : AstNode(token)
    {
        public readonly AstString PathNode = pathNode;
        public string Path => (string)PathNode.Value;

        public override string StrHelper(int level = 0, string note = "", bool isLeft = true)
        {
            string baseStr = base.StrHelper(level, note, false);
            string pathStr = PathNode?.StrHelper(level + 1, "Path: ");
            return baseStr + $"{new string(' ', 4 * (level + 1))}\n{pathStr}";
        }
    }
}
