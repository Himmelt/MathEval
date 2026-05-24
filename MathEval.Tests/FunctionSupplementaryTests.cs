using MathEval.Context;
using MathEval.Exceptions;
using Xunit;

namespace MathEval.Tests;

/// <summary>
/// 内置函数补充测试，覆盖 PRD 2.7 节中的遗漏场景
/// </summary>
public class FunctionSupplementaryTests {
    private readonly ExpressionContext _ctx = new();

    #region abs 函数

    [Fact]
    public void Abs_Zero_ReturnsZero() {
        Assert.Equal(0L, Expression.Eval<long>("abs(0)", _ctx));
    }

    [Fact]
    public void Abs_PositiveLong_ReturnsSame() {
        Assert.Equal(42L, Expression.Eval<long>("abs(42)", _ctx));
    }

    [Fact]
    public void Abs_NegativeDouble_ReturnsPositive() {
        Assert.Equal(3.14, Expression.Eval<double>("abs(-3.14)", _ctx), 2);
    }

    #endregion

    #region sqrt 函数

    [Fact]
    public void Sqrt_Zero_ReturnsZero() {
        Assert.Equal(0.0, Expression.Eval<double>("sqrt(0)", _ctx), 10);
    }

    [Fact]
    public void Sqrt_One_ReturnsOne() {
        Assert.Equal(1.0, Expression.Eval<double>("sqrt(1)", _ctx), 10);
    }

    [Fact]
    public void Sqrt_LargeNumber() {
        Assert.Equal(100.0, Expression.Eval<double>("sqrt(10000)", _ctx), 10);
    }

    #endregion

    #region 三角函数

    [Fact]
    public void Sin_PI_ReturnsZero() {
        Assert.Equal(0.0, Expression.Eval<double>("sin(PI)", _ctx), 10);
    }

    [Fact]
    public void Cos_PI_ReturnsMinusOne() {
        Assert.Equal(-1.0, Expression.Eval<double>("cos(PI)", _ctx), 10);
    }

    [Fact]
    public void Tan_PI_ReturnsZero() {
        Assert.Equal(0.0, Expression.Eval<double>("tan(PI)", _ctx), 10);
    }

    [Fact]
    public void Asin_OutOfRangeNegative_Throws() {
        Assert.Throws<EvaluateException>(() => Expression.Eval("asin(-2)", _ctx));
    }

    [Fact]
    public void Acos_OutOfRangeNegative_Throws() {
        Assert.Throws<EvaluateException>(() => Expression.Eval("acos(-2)", _ctx));
    }

    [Fact]
    public void Atan_LargeNumber() {
        Assert.Equal(Math.PI / 2, Expression.Eval<double>("atan(1e10)", _ctx), 5);
    }

    [Fact]
    public void Atan2_BothPositive() {
        Assert.Equal(Math.PI / 4, Expression.Eval<double>("atan2(1, 1)", _ctx), 10);
    }

    [Fact]
    public void Atan2_NegativeY() {
        Assert.Equal(-Math.PI / 4, Expression.Eval<double>("atan2(-1, 1)", _ctx), 10);
    }

    #endregion

    #region 对数函数

    [Fact]
    public void Ln_One_ReturnsZero() {
        Assert.Equal(0.0, Expression.Eval<double>("ln(1)", _ctx), 10);
    }

    [Fact]
    public void Log_One_ReturnsZero() {
        Assert.Equal(0.0, Expression.Eval<double>("log(1)", _ctx), 10);
    }

    [Fact]
    public void Log10_One_ReturnsZero() {
        Assert.Equal(0.0, Expression.Eval<double>("log10(1)", _ctx), 10);
    }

    [Fact]
    public void Log2_One_ReturnsZero() {
        Assert.Equal(0.0, Expression.Eval<double>("log2(1)", _ctx), 10);
    }

    [Fact]
    public void Log10_Negative_Throws() {
        Assert.Throws<EvaluateException>(() => Expression.Eval("log10(-1)", _ctx));
    }

    [Fact]
    public void Log2_Zero_Throws() {
        Assert.Throws<EvaluateException>(() => Expression.Eval("log2(0)", _ctx));
    }

    [Fact]
    public void Ln_Negative_Throws() {
        Assert.Throws<EvaluateException>(() => Expression.Eval("ln(-1)", _ctx));
    }

    #endregion

    #region exp 函数

    [Fact]
    public void Exp_One_ReturnsE() {
        Assert.Equal(Math.E, Expression.Eval<double>("exp(1)", _ctx), 10);
    }

    [Fact]
    public void Exp_NegativeOne_ReturnsOneOverE() {
        Assert.Equal(1.0 / Math.E, Expression.Eval<double>("exp(-1)", _ctx), 10);
    }

    #endregion

    #region 取整函数

    [Fact]
    public void Ceil_NegativeNumber() {
        Assert.Equal(-3L, Expression.Eval<long>("ceil(-3.8)", _ctx));
    }

    [Fact]
    public void Ceil_Integer_ReturnsSame() {
        Assert.Equal(5L, Expression.Eval<long>("ceil(5)", _ctx));
    }

    [Fact]
    public void Floor_NegativeNumber() {
        Assert.Equal(-4L, Expression.Eval<long>("floor(-3.2)", _ctx));
    }

    [Fact]
    public void Floor_Integer_ReturnsSame() {
        Assert.Equal(5L, Expression.Eval<long>("floor(5)", _ctx));
    }

    [Fact]
    public void Truncate_NegativeNumber() {
        Assert.Equal(-3L, Expression.Eval<long>("truncate(-3.9)", _ctx));
    }

    [Fact]
    public void Truncate_PositiveNumber() {
        Assert.Equal(3L, Expression.Eval<long>("truncate(3.9)", _ctx));
    }

    [Fact]
    public void Truncate_Integer_ReturnsSame() {
        Assert.Equal(5L, Expression.Eval<long>("truncate(5)", _ctx));
    }

    #endregion

    #region round 函数

    [Fact]
    public void Round_NegativeNumber() {
        Assert.Equal(-4L, Expression.Eval<long>("round(-3.5)", _ctx));
    }

    [Fact]
    public void Round_Down_NegativeNumber() {
        Assert.Equal(-3L, Expression.Eval<long>("round(-3.4)", _ctx));
    }

    [Fact]
    public void Round_WithDigits_Zero() {
        Assert.Equal(3.0, Expression.Eval<double>("round(3.14159, 0)", _ctx), 0);
    }

    [Fact]
    public void Round_WithDigits_Three() {
        Assert.Equal(3.142, Expression.Eval<double>("round(3.14159, 3)", _ctx), 3);
    }

    #endregion

    #region sign 函数

    [Fact]
    public void Sign_PositiveDouble() {
        Assert.Equal(1L, Expression.Eval<long>("sign(3.14)", _ctx));
    }

    [Fact]
    public void Sign_NegativeDouble() {
        Assert.Equal(-1L, Expression.Eval<long>("sign(-3.14)", _ctx));
    }

    [Fact]
    public void Sign_ZeroDouble() {
        Assert.Equal(0L, Expression.Eval<long>("sign(0.0)", _ctx));
    }

    #endregion

    #region max 和 min 函数

    [Fact]
    public void Max_DoubleDouble() {
        Assert.Equal(5.5, Expression.Eval<double>("max(3.14, 5.5)", _ctx), 1);
    }

    [Fact]
    public void Max_EqualValues() {
        Assert.Equal(5L, Expression.Eval<long>("max(5, 5)", _ctx));
    }

    [Fact]
    public void Min_LongLong() {
        Assert.Equal(3L, Expression.Eval<long>("min(3, 5)", _ctx));
    }

    [Fact]
    public void Min_DoubleLong() {
        Assert.Equal(2.0, Expression.Eval<double>("min(3.14, 2)", _ctx), 1);
    }

    [Fact]
    public void Min_EqualValues() {
        Assert.Equal(5L, Expression.Eval<long>("min(5, 5)", _ctx));
    }

    #endregion

    #region pow 函数

    [Fact]
    public void Pow_ZeroToZero_ReturnsOne() {
        Assert.Equal(1.0, Expression.Eval<double>("pow(0, 0)", _ctx), 10);
    }

    [Fact]
    public void Pow_NegativeBaseIntegerExponent() {
        Assert.Equal(-8.0, Expression.Eval<double>("pow(-2, 3)", _ctx), 10);
    }

    [Fact]
    public void Pow_NegativeBaseFractionalExponent_Throws() {
        Assert.Throws<EvaluateException>(() => Expression.Eval("pow(-4, 0.5)", _ctx));
    }

    [Fact]
    public void Pow_NegativeExponent() {
        Assert.Equal(0.25, Expression.Eval<double>("pow(2, -2)", _ctx), 10);
    }

    [Fact]
    public void Pow_FractionalExponent() {
        Assert.Equal(3.0, Expression.Eval<double>("pow(9, 0.5)", _ctx), 10);
    }

    #endregion

    #region 常量

    [Fact]
    public void Constant_PI_HighPrecision() {
        Assert.Equal(Math.PI, Expression.Eval<double>("PI", _ctx), 14);
        Assert.Equal(Math.PI, Expression.Eval<double>("π", _ctx), 14);
    }

    [Fact]
    public void Constant_E_HighPrecision() {
        Assert.Equal(Math.E, Expression.Eval<double>("E", _ctx), 14);
    }

    [Fact]
    public void Constant_NaN_IsNaN() {
        Assert.True(double.IsNaN(Expression.Eval<double>("NaN", _ctx)));
    }

    [Fact]
    public void Constant_INF_IsPositiveInfinity() {
        Assert.True(double.IsPositiveInfinity(Expression.Eval<double>("INF", _ctx)));
    }

    [Fact]
    public void Constant_PI_Override() {
        _ctx.Set("PI", 3.0);
        Assert.Equal(3.0, Expression.Eval<double>("PI", _ctx), 10);
        _ctx.Set("π", 4.56);
        Assert.Equal(4.56, Expression.Eval<double>("π", _ctx), 10);
    }

    [Fact]
    public void Constant_NaN_Override_ThrowsInvalidOperationException() {
        Assert.Throws<Exceptions.InvalidOperationException>(() => _ctx.Set("NaN", 1.0));
    }

    [Fact]
    public void Constant_INF_Override_ThrowsInvalidOperationException() {
        Assert.Throws<Exceptions.InvalidOperationException>(() => _ctx.Set("INF", 1.0));
    }

    #endregion

    #region 函数注册

    [Fact]
    public void CustomFunction_WeakTyped() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("add", args => Convert.ToDouble(args[0]) + Convert.ToDouble(args[1]));
        Assert.Equal(7.0, Expression.Eval<double>("add(3, 4)", ctx), 10);
    }

    [Fact]
    public void CustomFunction_StrongTyped_3Args() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("clamp", (Func<double, double, double, double>)((v, min, max) => Math.Max(min, Math.Min(max, v))));
        Assert.Equal(5.0, Expression.Eval<double>("clamp(10, 1, 5)", ctx), 10);
    }

    [Fact]
    public void CustomFunction_Override() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("myFunc", (Func<double, double>)(x => x * 2));
        Assert.Equal(10.0, Expression.Eval<double>("myFunc(5)", ctx), 10);
        ctx.SetFunction("myFunc", (Func<double, double>)(x => x * 3));
        Assert.Equal(15.0, Expression.Eval<double>("myFunc(5)", ctx), 10);
    }

    [Fact]
    public void CustomFunction_Remove() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("myFunc", (Func<double, double>)(x => x * 2));
        ctx.Remove("myFunc");
        Assert.Throws<FunctionNotFoundException>(() => Expression.Eval("myFunc(5)", ctx));
    }

    [Fact]
    public void CustomFunction_ArgCountMismatch_Throws() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("add", (Func<double, double, double>)((a, b) => a + b));
        Assert.Throws<FunctionTypeMismatchException>(() => Expression.Eval("add(1, 2, 3)", ctx));
    }

    #endregion
}
