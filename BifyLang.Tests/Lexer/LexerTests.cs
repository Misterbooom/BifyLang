#nullable enable
using System.Linq;
using BifyLang.Lexer;
using BifyLang.Tests.Infrastructure;
using NUnit.Framework;

namespace BifyLang.Tests.Lexer;

[TestFixture]
public class LexerTests : BifyTestBase
{
    [Test]
    public void Lexer_TokenizesKeywordsCorrectly()
    {
        string code = "class new if else while for switch case default return const constructor break continue";
        var tokens = Bify.Lex(code);

        Assert.That(tokens.Any(t => t.Type == TokenType.CLASS), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.NEW), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.IF), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.ELSE), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.WHILE), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.FOR), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.SWITCH), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.CASE), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.DEFAULT), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.RETURN), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.CONST), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.CONSTRUCTOR), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.BREAK), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.CONTINUE), Is.True);
    }

    [Test]
    public void Lexer_TokenizesOperatorsAndPunctuation()
    {
        string code = "== != <= >= += -= *= /= ++ -- && || { } ( ) [ ] ; , . : = + - * / < >";
        var tokens = Bify.Lex(code);

        Assert.That(tokens.Any(t => t.Type == TokenType.EQ), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.NEQ), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.LTEQ), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.GTEQ), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.ADDE), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.SUBE), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.MULE), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.DIVE), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.INCREMENT), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.DECREMENT), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.AND), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.OR), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.ASSIGN), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.EOL), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.LCUR), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.RCUR), Is.True);
    }

    [Test]
    public void Lexer_TokenizesLiterals()
    {
        string code = @"42 3.14 ""hello world""";
        var tokens = Bify.Lex(code);

        var numTokens = tokens.Where(t => t.Type == TokenType.NUMBER).ToList();
        Assert.That(numTokens.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(numTokens.Any(t => t.Value == "42"), Is.True);
        Assert.That(numTokens.Any(t => t.Value == "3.14"), Is.True);

        var strToken = tokens.FirstOrDefault(t => t.Type == TokenType.STRING);
        Assert.That(strToken, Is.Not.Null);
        Assert.That(strToken!.Value, Is.EqualTo("hello world"));
    }

    [Test]
    public void Lexer_TracksLineAndColumn()
    {
        string code = "int a = 1;\nint b = 2;";
        var tokens = Bify.Lex(code);

        var firstToken = tokens[0];
        Assert.That(firstToken.Value, Is.EqualTo("int"));
        Assert.That(firstToken.Line, Is.EqualTo(1));
        Assert.That(firstToken.Column, Is.EqualTo(1));

        var secondLineToken = tokens.FirstOrDefault(t => t.Line == 2 && t.Value == "int");
        Assert.That(secondLineToken, Is.Not.Null);
        Assert.That(secondLineToken!.Column, Is.EqualTo(1));
    }
}
