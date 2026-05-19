using MathEval.Context;
using MathEval.Exceptions;
using Xunit;

namespace MathEval.Tests;

public class StringInterpolationTests {
    [Fact]
    public void BasicInterpolation() {
        var context = new ExpressionContext();
        context.Set("name", "World");
        var result = Expression.Eval("$\"Hello, {name}!\"", context);
        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public void ExpressionInterpolation() {
        var result = Expression.Eval("$\"2 + 3 = {2 + 3}\"");
        Assert.Equal("2 + 3 = 5", result);
    }

    [Fact]
    public void FormatSpecifier_F2() {
        var result = Expression.Eval("$\"Pi = {3.14159:F2}\"");
        Assert.Equal("Pi = 3.14", result);
    }

    [Fact]
    public void EscapedBraces() {
        var result = Expression.Eval("$\"{{not interpolated}}\"");
        Assert.Equal("{not interpolated}", result);
    }

    [Fact]
    public void NestedFunctionCall() {
        var result = Expression.Eval("$\"sqrt(4) = {sqrt(4)}\"");
        Assert.Equal("sqrt(4) = 2", result);
    }

    [Fact]
    public void SingleQuoteInterpolation() {
        var context = new ExpressionContext();
        context.Set("name", "World");
        var result = Expression.Eval("$'Hello {name}!'", context);
        Assert.Equal("Hello World!", result);
    }

    [Fact]
    public void FormatSpecifier_D5() {
        var result = Expression.Eval("$\"{42:D5}\"");
        Assert.Equal("00042", result);
    }

    [Fact]
    public void FormatSpecifier_X4() {
        var result = Expression.Eval("$\"{255:X4}\"");
        Assert.Equal("00FF", result);
    }

    [Fact]
    public void NonNumericWithFormat_Throws_EvaluateException() {
        Assert.Throws<EvaluateException>(() => Expression.Eval("$\"{'hello':F2}\""));
    }

    [Fact]
    public void UnsupportedFormat_Throws_ParseException() {
        Assert.Throws<ParseException>(() => Expression.Eval("$\"{42:Z5}\""));
    }
}