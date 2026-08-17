using MathEval.Context;
using MathEval.Exceptions;
using MathEval.Options;
using Xunit;
using TokenType = MathEval.Lexer.TokenType;

namespace MathEval.Tests.Evaluation;

/// <summary>
/// P3 插值字符串：词法、解析、求值、格式化与编译模式
/// </summary>
public class StringInterpolationTests {
    #region 词法

    [Fact]
    public void Lexer_InterpolatedToken_KeepsRawText() {
        var lexer = new MathEval.Lexer.Lexer("$'a {1} b'");
        lexer.MoveNext();
        Assert.Equal(TokenType.InterpolatedString, lexer.CurrentToken.Type);
        Assert.Equal("$'a {1} b'", lexer.CurrentToken.Text);
    }

    [Fact]
    public void Lexer_DoubleQuotedInterpolated() {
        var lexer = new MathEval.Lexer.Lexer("$\"x={x}\"");
        lexer.MoveNext();
        Assert.Equal(TokenType.InterpolatedString, lexer.CurrentToken.Type);
    }

    [Fact]
    public void Lexer_UnterminatedInterpolated_Throws() {
        Assert.Throws<SyntaxException>(() => {
            var lexer = new MathEval.Lexer.Lexer("$'abc {1");
            lexer.MoveNext();
        });
    }

    [Fact]
    public void Lexer_UnmatchedCloseBrace_Throws() {
        Assert.Throws<SyntaxException>(() => {
            var lexer = new MathEval.Lexer.Lexer("$'a } b'");
            lexer.MoveNext();
        });
    }

    [Fact]
    public void Lexer_EscapedBraces_AreLiteral() {
        // {{ }} 转义不视为插值边界，Token 原文保留转义
        var lexer = new MathEval.Lexer.Lexer("$'{{not-interp}}'");
        lexer.MoveNext();
        Assert.Equal(TokenType.InterpolatedString, lexer.CurrentToken.Type);
        Assert.Equal("$'{{not-interp}}'", lexer.CurrentToken.Text);
    }

    [Fact]
    public void Lexer_NestedStringInsideInterpolation() {
        var lexer = new MathEval.Lexer.Lexer("$'{'a'} b'");
        lexer.MoveNext();
        Assert.Equal(TokenType.InterpolatedString, lexer.CurrentToken.Type);
        Assert.Equal("$'{'a'} b'", lexer.CurrentToken.Text);
    }

    #endregion

    #region 基础求值

    [Fact]
    public void Interpolated_WithoutExpressions() {
        Assert.Equal("hello", Expression.Eval<string>("$'hello'"));
        Assert.Equal("hello", Expression.Eval<string>("$\"hello\""));
    }

    [Fact]
    public void Interpolated_EmptyString() {
        Assert.Equal(string.Empty, Expression.Eval<string>("$''"));
    }

    [Fact]
    public void Interpolated_SingleExpression() {
        Assert.Equal("2", Expression.Eval<string>("$'{1+1}'"));
    }

    [Fact]
    public void Interpolated_MixedTextAndExpressions() {
        Assert.Equal("1 + 2 = 3", Expression.Eval<string>("$'{1} + {2} = {1+2}'"));
    }

    [Fact]
    public void Interpolated_ContextVariable() {
        var context = new ExpressionContext();
        context.Set("name", "World");
        Assert.Equal("Hello, World!", Expression.Eval<string>("$'Hello, {name}!'", context));
    }

    [Fact]
    public void Interpolated_EscapedBraces_RenderLiteralBraces() {
        Assert.Equal("{not-interp}", Expression.Eval<string>("$'{{not-interp}}'"));
        Assert.Equal("a { b", Expression.Eval<string>("$'a {{ b'"));
    }

    [Fact]
    public void Interpolated_NestedStringExpression() {
        Assert.Equal("ab", Expression.Eval<string>("$'{'a' + 'b'}'"));
    }

    [Fact]
    public void Interpolated_FunctionCallInside() {
        var context = new ExpressionContext();
        context.SetFunction("upper", (string s) => s.ToUpperInvariant());
        Assert.Equal("HI bob", Expression.Eval<string>("$'{upper('hi')} bob'", context));
    }

    [Fact]
    public void Interpolated_ConditionalInside() {
        Assert.Equal("big", Expression.Eval<string>("$'{10 > 5 ? 'big' : 'small'}'"));
    }

    [Fact]
    public void Interpolated_ResultIsText_CanConcat() {
        Assert.Equal("v=2!", Expression.Eval<string>("$'v={1+1}' + '!'"));
    }

    [Fact]
    public void Interpolated_InsideArrayLiteral() {
        var result = Expression.Eval<string[]>("['a', $'x{1}']");
        Assert.Equal(new[] { "a", "x1" }, result);
    }

    #endregion

    #region 数值显示与格式化

    [Fact]
    public void Interpolated_NumberDefaultFormat_IsG() {
        Assert.Equal("1.5", Expression.Eval<string>("$'{1.5}'"));
        Assert.Equal("1000000", Expression.Eval<string>("$'{1e6}'"));
    }

    [Fact]
    public void Interpolated_SpecialValues() {
        Assert.Equal("NaN", Expression.Eval<string>("$'{NaN}'"));
        Assert.Equal("INF", Expression.Eval<string>("$'{INF}'"));
    }

    [Theory]
    [InlineData("$'{3.14159:f2}'", "3.14")]
    [InlineData("$'{42:d}'", "42")]
    [InlineData($@"$'{{255:x}}'", "ff")]
    [InlineData("$'{1e10:e2}'", "1.00e+010")]
    [InlineData("$'{1 > 0 ? 3.14159 : 0:f1}'", "3.1")]
    public void Interpolated_FormatSpecifiers(string expr, string expected) {
        Assert.Equal(expected, Expression.Eval<string>(expr));
    }

    [Fact]
    public void Interpolated_FormatInsideLargerText() {
        Assert.Equal("pi=3.14!", Expression.Eval<string>("$'pi={3.14159:f2}!'"));
    }

    [Fact]
    public void Interpolated_FormatOnText_Throws() {
        Assert.Throws<EvaluationException>(() => Expression.Eval("$'{'a':f2}'"));
    }

    [Fact]
    public void Interpolated_IntegerFormatOnFraction_Throws() {
        Assert.Throws<EvaluationException>(() => Expression.Eval("$'{1.5:d}'"));
    }

    [Fact]
    public void Interpolated_UnsupportedFormat_Throws() {
        Assert.Throws<SyntaxException>(() => Expression.Eval("$'{1:q}'"));
    }

    [Fact]
    public void Interpolated_TextValue_NoFormat() {
        Assert.Equal("a b", Expression.Eval<string>("$'{'a b'}'"));
    }

    #endregion

    #region 编译模式与常量折叠

    [Fact]
    public void Compiled_InterpolatedBasic() {
        Assert.Equal("1 + 2 = 3", Expression.OptimizedEval<string>("$'{1} + {2} = {1+2}'"));
    }

    [Fact]
    public void Compiled_InterpolatedContextVariable() {
        var context = new ExpressionContext();
        context.Set("name", "World");
        Assert.Equal("Hello, World!", Expression.OptimizedEval<string>("$'Hello, {name}!'", context));
    }

    [Fact]
    public void Compiled_InterpolatedFormat() {
        Assert.Equal("pi=3.14", Expression.OptimizedEval<string>("$'pi={3.14159:f2}'"));
    }

    [Fact]
    public void Compiled_InterpolatedFormatError_Throws() {
        Assert.Throws<EvaluationException>(() => Expression.OptimizedEval("$'{'a':f2}'"));
    }

    [Fact]
    public void ConstantFolding_FoldsAllConstantInterpolation() {
        Assert.Equal("v=3", Expression.Eval<string>("$'v={1+2}'", null,
            ExpressionOptions.ConstantFolding));
    }

    [Fact]
    public void ConstantFolding_FoldsConstantSegment_KeepsVariable() {
        var context = new ExpressionContext();
        context.Set("x", 10.0);
        Assert.Equal("1+2=3,x=10", Expression.Eval<string>("$'{1}+{2}={1+2},x={x}'", context,
            ExpressionOptions.ConstantFolding));
    }

    #endregion
}
