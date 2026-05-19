using MathEval;
using MathEval.Context;
using MathEval.Exceptions;
using Xunit;

namespace MathEval.Tests;

public class FunctionTests
{
    private readonly ExpressionContext _ctx = new();

    [Fact]
    public void Abs_Long_ReturnsLong()
    {
        var result = Expression.Eval("abs(-42)", _ctx);
        Assert.IsType<long>(result);
        Assert.Equal(42L, (long)result);
    }

    [Fact]
    public void Abs_Double_ReturnsDouble()
    {
        var result = Expression.Eval("abs(-3.14)", _ctx);
        Assert.IsType<double>(result);
        Assert.Equal(3.14, (double)result, 2);
    }

    [Fact]
    public void Sqrt_Positive_ReturnsDouble()
    {
        Assert.Equal(2.0, Expression.Eval<double>("sqrt(4)", _ctx), 10);
    }

    [Fact]
    public void Sqrt_Negative_ThrowsEvaluateException()
    {
        Assert.Throws<EvaluateException>(() => Expression.Eval("sqrt(-1)", _ctx));
    }

    [Fact]
    public void Sin_Zero()
    {
        Assert.Equal(0.0, Expression.Eval<double>("sin(0)", _ctx), 10);
    }

    [Fact]
    public void Cos_Zero()
    {
        Assert.Equal(1.0, Expression.Eval<double>("cos(0)", _ctx), 10);
    }

    [Fact]
    public void Tan_Zero()
    {
        Assert.Equal(0.0, Expression.Eval<double>("tan(0)", _ctx), 10);
    }

    [Fact]
    public void Asin_One()
    {
        Assert.Equal(Math.PI / 2, Expression.Eval<double>("asin(1)", _ctx), 10);
    }

    [Fact]
    public void Acos_One()
    {
        Assert.Equal(0.0, Expression.Eval<double>("acos(1)", _ctx), 10);
    }

    [Fact]
    public void Asin_OutOfRange_ThrowsEvaluateException()
    {
        Assert.Throws<EvaluateException>(() => Expression.Eval("asin(2)", _ctx));
    }

    [Fact]
    public void Atan_Zero()
    {
        Assert.Equal(0.0, Expression.Eval<double>("atan(0)", _ctx), 10);
    }

    [Fact]
    public void Atan2_ZeroOne()
    {
        Assert.Equal(0.0, Expression.Eval<double>("atan2(0, 1)", _ctx), 10);
    }

    [Fact]
    public void Exp_Zero()
    {
        Assert.Equal(1.0, Expression.Eval<double>("exp(0)", _ctx), 10);
    }

    [Fact]
    public void Ln_E()
    {
        Assert.Equal(1.0, Expression.Eval<double>("ln(E)", _ctx), 5);
    }

    [Fact]
    public void Log_E()
    {
        Assert.Equal(1.0, Expression.Eval<double>("log(E)", _ctx), 5);
    }

    [Fact]
    public void Log10_Ten()
    {
        Assert.Equal(1.0, Expression.Eval<double>("log10(10)", _ctx), 10);
    }

    [Fact]
    public void Log2_Two()
    {
        Assert.Equal(1.0, Expression.Eval<double>("log2(2)", _ctx), 10);
    }

    [Fact]
    public void Ln_Zero_ThrowsEvaluateException()
    {
        Assert.Throws<EvaluateException>(() => Expression.Eval("ln(0)", _ctx));
    }

    [Fact]
    public void Log_Negative_ThrowsEvaluateException()
    {
        Assert.Throws<EvaluateException>(() => Expression.Eval("log(-1)", _ctx));
    }

    [Fact]
    public void Ceil_ReturnsLong()
    {
        var result = Expression.Eval("ceil(3.2)", _ctx);
        Assert.IsType<long>(result);
        Assert.Equal(4L, (long)result);
    }

    [Fact]
    public void Floor_ReturnsLong()
    {
        var result = Expression.Eval("floor(3.8)", _ctx);
        Assert.IsType<long>(result);
        Assert.Equal(3L, (long)result);
    }

    [Fact]
    public void Round_Midpoint()
    {
        var result = Expression.Eval("round(3.5)", _ctx);
        Assert.IsType<long>(result);
        Assert.Equal(4L, (long)result);
    }

    [Fact]
    public void Round_Down()
    {
        var result = Expression.Eval("round(3.4)", _ctx);
        Assert.IsType<long>(result);
        Assert.Equal(3L, (long)result);
    }

    [Fact]
    public void Round_WithDigits()
    {
        Assert.Equal(3.14, Expression.Eval<double>("round(3.14159, 2)", _ctx), 2);
    }

    [Fact]
    public void Truncate_ReturnsLong()
    {
        var result = Expression.Eval("truncate(3.9)", _ctx);
        Assert.IsType<long>(result);
        Assert.Equal(3L, (long)result);
    }

    [Fact]
    public void Sign_Negative()
    {
        var result = Expression.Eval("sign(-5)", _ctx);
        Assert.IsType<long>(result);
        Assert.Equal(-1L, (long)result);
    }

    [Fact]
    public void Sign_Zero()
    {
        var result = Expression.Eval("sign(0)", _ctx);
        Assert.IsType<long>(result);
        Assert.Equal(0L, (long)result);
    }

    [Fact]
    public void Sign_Positive()
    {
        var result = Expression.Eval("sign(5)", _ctx);
        Assert.IsType<long>(result);
        Assert.Equal(1L, (long)result);
    }

    [Fact]
    public void Max_LongLong()
    {
        var result = Expression.Eval("max(3, 5)", _ctx);
        Assert.IsType<long>(result);
        Assert.Equal(5L, (long)result);
    }

    [Fact]
    public void Max_LongDouble()
    {
        var result = Expression.Eval("max(3, 5.5)", _ctx);
        Assert.IsType<double>(result);
        Assert.Equal(5.5, (double)result, 1);
    }

    [Fact]
    public void Min_DoubleLong()
    {
        var result = Expression.Eval("min(3.14, 2)", _ctx);
        Assert.IsType<double>(result);
        Assert.Equal(2.0, (double)result, 1);
    }

    [Fact]
    public void Pow()
    {
        Assert.Equal(8.0, Expression.Eval<double>("pow(2, 3)", _ctx), 10);
    }

    [Fact]
    public void Constant_PI()
    {
        Assert.Equal(3.14159, Expression.Eval<double>("PI", _ctx), 5);
    }

    [Fact]
    public void Constant_E()
    {
        Assert.Equal(2.718, Expression.Eval<double>("E", _ctx), 3);
    }

    [Fact]
    public void StrongTyped_Function()
    {
        var ctx = new ExpressionContext();
        ctx.SetFunction("doubleIt", (Func<double, double>)(x => x * 2));
        Assert.Equal(10.0, Expression.Eval<double>("doubleIt(5)", ctx), 10);
    }

    [Fact]
    public void Delegate_Function()
    {
        var ctx = new ExpressionContext();
        ctx.SetFunction("mul", (Delegate)(Func<double, double, double>)((a, b) => a * b));
        Assert.Equal(12.0, Expression.Eval<double>("mul(3, 4)", ctx), 10);
    }

    [Fact]
    public void Function_ArgumentCountMismatch_ThrowsFunctionTypeMismatchException()
    {
        var ctx = new ExpressionContext();
        ctx.SetFunction("doubleIt", (Func<double, double>)(x => x * 2));
        Assert.Throws<FunctionTypeMismatchException>(() => Expression.Eval("doubleIt(1, 2)", ctx));
    }

    [Fact]
    public void Function_ArgumentTypeMismatch_ThrowsFunctionTypeMismatchException()
    {
        var ctx = new ExpressionContext();
        ctx.SetFunction<Guid, string>("badFunc", x => x.ToString());
        Assert.Throws<FunctionTypeMismatchException>(() => Expression.Eval("badFunc(1)", ctx));
    }
}
