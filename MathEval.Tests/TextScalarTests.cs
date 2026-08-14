using MathEval.Context;
using MathEval.Exceptions;
using Xunit;

using Token = MathEval.Lexer.Token;
using TokenType = MathEval.Lexer.TokenType;

namespace MathEval.Tests;

/// <summary>
/// P1 文本标量：字符串字面量、拼接、比较与自定义函数字符串参数
/// </summary>
public class TextScalarTests {
    private static Token GetSingleToken(string text) {
        var lexer = new Lexer.Lexer(text);
        lexer.MoveNext();
        return lexer.CurrentToken;
    }

    #region 词法

    [Fact]
    public void Lexer_SingleQuotedString() {
        var token = GetSingleToken("'hello'");
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("hello", token.Text);
    }

    [Fact]
    public void Lexer_DoubleQuotedString() {
        var token = GetSingleToken("\"hello\"");
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("hello", token.Text);
    }

    [Fact]
    public void Lexer_EmptyString() {
        var token = GetSingleToken("''");
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal(string.Empty, token.Text);
    }

    [Fact]
    public void Lexer_EscapeSequences() {
        var token = GetSingleToken("'a\\n\\t\\\\\\'\\\"'");
        Assert.Equal("a\n\t\\'\"", token.Text);
    }

    [Fact]
    public void Lexer_HexEscape() {
        var token = GetSingleToken("'\\x41'");
        Assert.Equal("A", token.Text);
    }

    [Fact]
    public void Lexer_UnicodeEscape() {
        var token = GetSingleToken("'\\u4e2d'");
        Assert.Equal("中", token.Text);
    }

    [Fact]
    public void Lexer_UnterminatedString_Throws() {
        Assert.Throws<ParseException>(() => GetSingleToken("'abc"));
    }

    [Fact]
    public void Lexer_InvalidEscape_Throws() {
        Assert.Throws<ParseException>(() => GetSingleToken("'\\q'"));
    }

    [Fact]
    public void Lexer_StringInsideExpression() {
        var lexer = new Lexer.Lexer("'a' + 1");
        lexer.MoveNext();
        Assert.Equal(TokenType.String, lexer.CurrentToken.Type);
        lexer.MoveNext();
        Assert.Equal(TokenType.Plus, lexer.CurrentToken.Type);
        lexer.MoveNext();
        Assert.Equal(TokenType.Number, lexer.CurrentToken.Type);
    }

    #endregion

    #region 求值：字面量与拼接

    [Fact]
    public void StringLiteral_ReturnsText() {
        Assert.Equal("hello", Expression.Eval<string>("'hello'"));
        Assert.Equal("hello", Expression.Eval<string>("\"hello\""));
    }

    [Fact]
    public void StringConcat_TwoOperands() {
        Assert.Equal("ab", Expression.Eval<string>("'a' + 'b'"));
    }

    [Fact]
    public void StringConcat_Chained() {
        Assert.Equal("abc", Expression.Eval<string>("'a' + 'b' + 'c'"));
    }

    [Fact]
    public void StringConcat_WithContextVariable() {
        var context = new ExpressionContext();
        context.Set("name", "World");
        Assert.Equal("Hello, World!", Expression.Eval<string>("'Hello, ' + name + '!'", context));
    }

    [Fact]
    public void StringLiteral_WithEscapes() {
        Assert.Equal("a\nb", Expression.Eval<string>("'a\\nb'"));
    }

    [Fact]
    public void StringEval_NonGeneric_ReturnsString() {
        Assert.Equal("hello", (string)Expression.Eval("'hello'"));
    }

    #endregion

    #region 求值：比较（序数比较，返回 1.0/0.0）

    [Theory]
    [InlineData("'a' == 'a'", 1.0)]
    [InlineData("'a' == 'b'", 0.0)]
    [InlineData("'a' != 'b'", 1.0)]
    [InlineData("'a' != 'a'", 0.0)]
    [InlineData("'a' < 'b'", 1.0)]
    [InlineData("'b' < 'a'", 0.0)]
    [InlineData("'abc' <= 'abc'", 1.0)]
    [InlineData("'abd' <= 'abc'", 0.0)]
    [InlineData("'b' > 'a'", 1.0)]
    [InlineData("'a' > 'b'", 0.0)]
    [InlineData("'abc' >= 'abd'", 0.0)]
    [InlineData("'abd' >= 'abc'", 1.0)]
    [InlineData("'' == ''", 1.0)]
    public void StringComparison_ReturnsNumber(string expr, double expected) {
        Assert.Equal(expected, Expression.Eval<double>(expr));
    }

    [Fact]
    public void StringComparison_IsOrdinal() {
        // 序数比较：'a'(0x61) < 'B'(0x42) 为 false，与区域文化无关
        Assert.Equal(0.0, Expression.Eval<double>("'a' < 'B'"));
    }

    #endregion

    #region 自定义函数字符串参数

    [Fact]
    public void CustomFunction_StringParameter() {
        var context = new ExpressionContext();
        context.SetFunction("upper", (string s) => s.ToUpperInvariant());
        Assert.Equal("HI", Expression.Eval<string>("upper('hi')", context));
    }

    [Fact]
    public void CustomFunction_StringParameterWithConcat() {
        var context = new ExpressionContext();
        context.SetFunction("greet", (string name, string suffix) => "Hi " + name + suffix);
        Assert.Equal("Hi Bob!", Expression.Eval<string>("greet('Bob', '!')", context));
    }

    [Fact]
    public void CustomFunction_StringReturn_NumberArg() {
        var context = new ExpressionContext();
        context.SetFunction("repeat", (double n) => new string('*', (int)n));
        Assert.Equal("***", Expression.Eval<string>("repeat(3)", context));
    }

    #endregion

    #region 类型错误（严格 Kind）

    [Theory]
    [InlineData("'a' * 'b'")]
    [InlineData("'a' - 'b'")]
    [InlineData("'a' / 'b'")]
    [InlineData("'a' + 1")]
    [InlineData("1 + 'a'")]
    [InlineData("'a' == 1")]
    [InlineData("-'a'")]
    public void TextWithUnsupportedOperator_Throws(string expr) {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval(expr));
    }

    [Fact]
    public void TextInLogicalContext_Throws() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("'a' and 'b'"));
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("'a' ? 1 : 0"));
    }

    [Fact]
    public void TextIsNotIndexable_Throws() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("'abc'[0]"));
    }

    #endregion

    #region 编译模式一致性

    [Fact]
    public void Compiled_StringLiteralAndConcat() {
        Assert.Equal("hello", Expression.OptimizedEval<string>("'hello'"));
        Assert.Equal("ab", Expression.OptimizedEval<string>("'a' + 'b'"));
    }

    [Fact]
    public void Compiled_StringComparison() {
        Assert.Equal(1.0, Expression.OptimizedEval<double>("'a' == 'a'"));
        Assert.Equal(0.0, Expression.OptimizedEval<double>("'a' < 'B'"));
    }

    [Fact]
    public void Compiled_CustomFunction_StringParameter() {
        var context = new ExpressionContext();
        context.SetFunction("upper", (string s) => s.ToUpperInvariant());
        Assert.Equal("HI", Expression.OptimizedEval<string>("upper('hi')", context));
    }

    [Fact]
    public void Compiled_TextOperatorMismatch_Throws() {
        Assert.Throws<TypeMismatchException>(() => Expression.OptimizedEval("'a' * 'b'"));
    }

    [Fact]
    public void ConstantFolding_FoldsTextConcat() {
        // 常量折叠在编译期完成 'a'+'b' → 'ab'
        Assert.Equal("ab", Expression.Eval<string>("'a' + 'b'", null,
            MathEval.ExpressionOptions.ConstantFolding));
    }

    #endregion
}
