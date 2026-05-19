using MathEval.Exceptions;
using Xunit;

using Token = MathEval.Lexer.Token;
using TokenType = MathEval.Lexer.TokenType;

namespace MathEval.Tests;

public class LexerTests {
    private static Token GetSingleToken(string text) {
        var lexer = new Lexer.Lexer(text);
        lexer.MoveNext();
        return lexer.CurrentToken;
    }

    [Fact]
    public void DecimalInteger() {
        var token = GetSingleToken("42");
        Assert.Equal(TokenType.Integer, token.Type);
        Assert.Equal("42", token.Text);
    }

    [Fact]
    public void DecimalFloat() {
        var token = GetSingleToken("3.14");
        Assert.Equal(TokenType.Float, token.Type);
        Assert.Equal("3.14", token.Text);
    }

    [Fact]
    public void ScientificNotation_PositiveExponent() {
        var token = GetSingleToken("1e5");
        Assert.Equal(TokenType.Float, token.Type);
        Assert.Equal("1e5", token.Text);
    }

    [Fact]
    public void ScientificNotation_NegativeExponent() {
        var token = GetSingleToken("1e-5");
        Assert.Equal(TokenType.Float, token.Type);
        Assert.Equal("1e-5", token.Text);
    }

    [Fact]
    public void HexNumber() {
        var token = GetSingleToken("0xFF");
        Assert.Equal(TokenType.Integer, token.Type);
        Assert.Equal("0xFF", token.Text);
    }

    [Fact]
    public void OctalNumber() {
        var token = GetSingleToken("0o77");
        Assert.Equal(TokenType.Integer, token.Type);
        Assert.Equal("0o77", token.Text);
    }

    [Fact]
    public void BinaryNumber() {
        var token = GetSingleToken("0b1010");
        Assert.Equal(TokenType.Integer, token.Type);
        Assert.Equal("0b1010", token.Text);
    }

    [Fact]
    public void StringSingleQuote() {
        var token = GetSingleToken("'hello'");
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("hello", token.Text);
    }

    [Fact]
    public void StringDoubleQuote() {
        var token = GetSingleToken("\"world\"");
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("world", token.Text);
    }

    [Fact]
    public void StringEscape_Newline() {
        var token = GetSingleToken("'\\n'");
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("\n", token.Text);
    }

    [Fact]
    public void StringEscape_Hex() {
        var token = GetSingleToken("'\\x41'");
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("A", token.Text);
    }

    [Fact]
    public void BooleanTrue() {
        var token = GetSingleToken("true");
        Assert.Equal(TokenType.Boolean, token.Type);
        Assert.Equal("true", token.Text);
    }

    [Fact]
    public void BooleanFalse() {
        var token = GetSingleToken("false");
        Assert.Equal(TokenType.Boolean, token.Type);
        Assert.Equal("false", token.Text);
    }

    [Fact]
    public void KeywordAnd() {
        var token = GetSingleToken("and");
        Assert.Equal(TokenType.AndKeyword, token.Type);
        Assert.Equal("and", token.Text);
    }

    [Fact]
    public void KeywordOr() {
        var token = GetSingleToken("or");
        Assert.Equal(TokenType.OrKeyword, token.Type);
        Assert.Equal("or", token.Text);
    }

    [Fact]
    public void KeywordNot() {
        var token = GetSingleToken("not");
        Assert.Equal(TokenType.NotKeyword, token.Type);
        Assert.Equal("not", token.Text);
    }

    [Fact]
    public void KeywordXor() {
        var token = GetSingleToken("xor");
        Assert.Equal(TokenType.XorKeyword, token.Type);
        Assert.Equal("xor", token.Text);
    }

    [Fact]
    public void NaN() {
        var token = GetSingleToken("NaN");
        Assert.Equal(TokenType.NaN, token.Type);
        Assert.Equal("NaN", token.Text);
    }

    [Fact]
    public void INF() {
        var token = GetSingleToken("INF");
        Assert.Equal(TokenType.INF, token.Type);
        Assert.Equal("INF", token.Text);
    }

    [Fact]
    public void Identifier_Simple() {
        var token = GetSingleToken("x");
        Assert.Equal(TokenType.Identifier, token.Type);
        Assert.Equal("x", token.Text);
    }

    [Fact]
    public void Identifier_Underscore() {
        var token = GetSingleToken("_var");
        Assert.Equal(TokenType.Identifier, token.Type);
        Assert.Equal("_var", token.Text);
    }

    [Theory]
    [InlineData("+", TokenType.Plus)]
    [InlineData("-", TokenType.Minus)]
    [InlineData("*", TokenType.Asterisk)]
    [InlineData("/", TokenType.Slash)]
    [InlineData("//", TokenType.DoubleSlash)]
    [InlineData("%", TokenType.Percent)]
    [InlineData("^", TokenType.Caret)]
    [InlineData("&", TokenType.Ampersand)]
    [InlineData("|", TokenType.Pipe)]
    [InlineData("<<", TokenType.LeftShift)]
    [InlineData(">>", TokenType.RightShift)]
    [InlineData("==", TokenType.Equal)]
    [InlineData("!=", TokenType.NotEqual)]
    [InlineData("<", TokenType.Less)]
    [InlineData(">", TokenType.Greater)]
    [InlineData("<=", TokenType.LessOrEqual)]
    [InlineData(">=", TokenType.GreaterOrEqual)]
    [InlineData("?", TokenType.QuestionMark)]
    [InlineData(":", TokenType.Colon)]
    [InlineData("(", TokenType.LeftParenthesis)]
    [InlineData(")", TokenType.RightParenthesis)]
    [InlineData(",", TokenType.Comma)]
    [InlineData("!", TokenType.Exclamation)]
    [InlineData("~", TokenType.Tilde)]
    public void Operators(string input, TokenType expectedType) {
        var token = GetSingleToken(input);
        Assert.Equal(expectedType, token.Type);
        Assert.Equal(input, token.Text);
    }

    [Fact]
    public void DoubleAmpersand() {
        var token = GetSingleToken("&&");
        Assert.Equal(TokenType.DoubleAmpersand, token.Type);
        Assert.Equal("&&", token.Text);
    }

    [Fact]
    public void DoublePipe() {
        var token = GetSingleToken("||");
        Assert.Equal(TokenType.DoublePipe, token.Type);
        Assert.Equal("||", token.Text);
    }

    [Fact]
    public void InterpolatedString() {
        var token = GetSingleToken("$\"Hello {name}!\"");
        Assert.Equal(TokenType.InterpolatedString, token.Type);
    }

    [Fact]
    public void Error_ExponentWithoutDigits() {
        Assert.Throws<ParseException>(() => GetSingleToken("2e"));
    }

    [Fact]
    public void Error_HexWithoutDigits() {
        Assert.Throws<ParseException>(() => GetSingleToken("0x"));
    }

    [Fact]
    public void Error_UnterminatedString() {
        Assert.Throws<ParseException>(() => GetSingleToken("'unterminated"));
    }

    [Fact]
    public void Error_InvalidEscape() {
        Assert.Throws<ParseException>(() => GetSingleToken("'\\z'"));
    }

    [Fact]
    public void Error_ExpressionExceedsMaxLength() {
        var longText = new string('1', 4097);
        Assert.Throws<ParseException>(() => new Lexer.Lexer(longText));
    }

    [Fact]
    public void TokenizeAll_ReturnsAllTokens() {
        var lexer = new Lexer.Lexer("1 + 2");
        var tokens = lexer.TokenizeAll();
        Assert.Equal(4, tokens.Count);
        Assert.Equal(TokenType.Integer, tokens[0].Type);
        Assert.Equal(TokenType.Plus, tokens[1].Type);
        Assert.Equal(TokenType.Integer, tokens[2].Type);
        Assert.Equal(TokenType.EOF, tokens[3].Type);
    }

    [Fact]
    public void Token_LineAndColumn() {
        var token = GetSingleToken("42");
        Assert.Equal(1, token.Line);
        Assert.Equal(1, token.Column);
    }
}