using MathEval.Context;
using MathEval.Exceptions;
using Xunit;

namespace MathEval.Tests;

/// <summary>
/// Expression.Eval 补充测试，覆盖 PRD 中的遗漏场景
/// </summary>
public class EvaluationSupplementaryTests {
    #region 布尔与数值比较

    [Fact]
    public void Comparison_BoolEqualNumber_ReturnsTrue() {
        // true == 1 is now 1.0 == 1.0 which is TRUE
        Assert.True(Expression.Eval<bool>("true == 1"));
    }

    [Fact]
    public void Comparison_BoolNotEqualNumber_ReturnsFalse() {
        // true != 1 is now 1.0 != 1.0 which is FALSE
        Assert.False(Expression.Eval<bool>("true != 1"));
    }

    [Fact]
    public void Comparison_FalseEqualZero_ReturnsTrue() {
        // false == 0 is now 0.0 == 0.0 which is TRUE
        Assert.True(Expression.Eval<bool>("false == 0"));
    }

    #endregion

    #region 位运算边界

    [Fact]
    public void Bitwise_RightShiftNegative_PreservesSign() {
        Assert.Equal(-2L, Expression.Eval<long>("-4 >> 1"));
    }

    [Fact]
    public void Bitwise_RightShift64_MasksToZero() {
        Assert.Equal(16L, Expression.Eval<long>("16 >> 64"));
    }

    [Fact]
    public void Bitwise_RightShiftNegative_ThrowsEvaluateException() {
        Assert.Throws<EvaluateException>(() => Expression.Eval("16 >> -1"));
    }

    [Fact]
    public void Bitwise_LeftShiftLargeValue() {
        Assert.Equal(1L << 63, Expression.Eval<long>("1 << 63"));
    }

    #endregion

    #region 溢出行为

    [Fact]
    public void Overflow_DoubleOverflow_ReturnsINF() {
        var result = Expression.Eval<double>("1e308 * 10");
        Assert.True(double.IsPositiveInfinity(result));
    }

    [Fact]
    public void Overflow_DoubleUnderflow_ReturnsZero() {
        var result = Expression.Eval<double>("1e-308 / 1e308");
        Assert.Equal(0.0, result);
    }

    #endregion

    #region 三元运算符补充

    [Fact]
    public void Ternary_BoolAndNumberBranches() {
        var ctx = new ExpressionContext();
        ctx.Set("flag", true);
        Assert.Equal(1L, Expression.Eval<long>("flag ? 1 : 0", ctx));
    }

    [Fact]
    public void Ternary_NestedInExpression() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 5L);
        Assert.Equal(6L, Expression.Eval<long>("1 + (x > 0 ? x : -x)", ctx));
    }

    #endregion

    #region 一元运算补充

    [Fact]
    public void Unary_NotNumber_ReturnsZero() {
        // !5 is now valid (non-zero is truthy, !5 returns 0.0)
        Assert.Equal(0.0, Expression.Eval<double>("!5"));
    }

    [Fact]
    public void Unary_DoubleNegation() {
        Assert.Equal(5L, Expression.Eval<long>("--5"));
    }

    [Fact]
    public void Unary_PlusNumber() {
        Assert.Equal(5L, Expression.Eval<long>("+5"));
    }

    #endregion

    #region 逻辑运算补充

    [Fact]
    public void Logical_BoolAndNumber_ReturnsValue() {
        // true and 1 is now valid (returns 1.0)
        Assert.Equal(1.0, Expression.Eval<double>("true and 1"));
    }

    [Fact]
    public void Logical_BoolOrNumber_ReturnsValue() {
        // false or 0 is now valid (returns 0.0)
        Assert.Equal(0.0, Expression.Eval<double>("false or 0"));
    }

    #endregion

    #region 数值类型推断

    [Fact]
    public void TypeInference_IntegerLiteral_IsDouble() {
        Assert.IsType<double>(Expression.Eval("42"));
    }

    [Fact]
    public void TypeInference_FloatLiteral_IsDouble() {
        Assert.IsType<double>(Expression.Eval("3.14"));
    }

    [Fact]
    public void TypeInference_ScientificNotation_IsDouble() {
        Assert.IsType<double>(Expression.Eval("1e5"));
    }

    [Fact]
    public void TypeInference_HexLiteral_IsDouble() {
        Assert.IsType<double>(Expression.Eval("0xFF"));
    }

    [Fact]
    public void TypeInference_Division_IsDouble() {
        Assert.IsType<double>(Expression.Eval("7 / 2"));
    }

    [Fact]
    public void TypeInference_IntegerDivision_IsDouble() {
        // Expression.Eval("7 // 2") returns double(3.0)
        Assert.IsType<double>(Expression.Eval("7 // 2"));
    }

    [Fact]
    public void TypeInference_Modulo_Integers_IsDouble() {
        Assert.IsType<double>(Expression.Eval("7 % 3"));
    }

    [Fact]
    public void TypeInference_Modulo_Double_IsDouble() {
        Assert.IsType<double>(Expression.Eval("7.5 % 2"));
    }

    [Fact]
    public void TypeInference_BitwiseOp_IsDouble() {
        // Expression.Eval("5 & 3") returns double(1.0)
        Assert.IsType<double>(Expression.Eval("5 & 3"));
    }

    #endregion

    #region 特殊值补充

    [Fact]
    public void SpecialValues_NaNTimesNumber_ReturnsNaN() {
        Assert.True(double.IsNaN(Expression.Eval<double>("NaN * 5")));
    }

    [Fact]
    public void SpecialValues_INFDividedByNumber_ReturnsINF() {
        Assert.True(double.IsPositiveInfinity(Expression.Eval<double>("INF / 2")));
    }

    [Fact]
    public void SpecialValues_NumberDividedByINF_ReturnsZero() {
        Assert.Equal(0.0, Expression.Eval<double>("5 / INF"));
    }

    [Fact]
    public void SpecialValues_NegativeINF() {
        Assert.True(double.IsNegativeInfinity(Expression.Eval<double>("-INF")));
    }

    [Fact]
    public void SpecialValues_INFTimesNegative_ReturnsNegativeINF() {
        // INF * (-2) 应返回 -INF
        Assert.True(double.IsNegativeInfinity(Expression.Eval<double>("INF * -2")));
    }

    [Fact]
    public void SpecialValues_NegativeINFPlusINF_ReturnsNaN() {
        // (-INF) + INF 应返回 NaN
        Assert.True(double.IsNaN(Expression.Eval<double>("-INF + INF")));
    }

    [Fact]
    public void SpecialValues_NegativeINFTimes2_ReturnsNegativeINF() {
        // (-INF) * 2 应返回 -INF
        Assert.True(double.IsNegativeInfinity(Expression.Eval<double>("-INF * 2")));
    }

    [Fact]
    public void SpecialValues_NegativeINFDividedByINF_ReturnsNaN() {
        // (-INF) / INF 应返回 NaN
        Assert.True(double.IsNaN(Expression.Eval<double>("-INF / INF")));
    }

    [Fact]
    public void SpecialValues_INFDividedByNegativeINF_ReturnsNaN() {
        // INF / (-INF) 应返回 NaN
        Assert.True(double.IsNaN(Expression.Eval<double>("INF / -INF")));
    }

    [Fact]
    public void SpecialValues_NegativeINFRemainderINF_ReturnsNaN() {
        // (-INF) % INF 应返回 NaN
        Assert.True(double.IsNaN(Expression.Eval<double>("-INF % INF")));
    }

    #endregion

    #region 上下文补充

    [Fact]
    public void Context_DelayedValue() {
        var ctx = new ExpressionContext();
        var counter = 0;
        ctx.Set("counter", () => ++counter);
        Assert.Equal(1L, Expression.Eval<long>("counter", ctx));
        Assert.Equal(2L, Expression.Eval<long>("counter", ctx));
    }

    [Fact]
    public void Context_OverrideSymbol() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 10L);
        Assert.Equal(10L, Expression.Eval<long>("x", ctx));
        ctx.Set("x", 20L);
        Assert.Equal(20L, Expression.Eval<long>("x", ctx));
    }

    [Fact]
    public void Context_RemoveSymbol() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 10L);
        ctx.RemoveSymbol("x");
        Assert.Throws<SymbolNotFoundException>(() => Expression.Eval("x", ctx));
    }

    [Fact]
    public void Context_Inheritance() {
        var parent = new ExpressionContext();
        parent.Set("x", 10L);
        var child = parent.CreateChild();
        Assert.Equal(10L, Expression.Eval<long>("x", child));
        child.Set("y", 20L);
        Assert.Equal(20L, Expression.Eval<long>("y", child));
        Assert.Throws<SymbolNotFoundException>(() => Expression.Eval("y", parent));
    }

    #endregion

    #region 错误处理补充

    [Fact]
    public void Error_WhitespaceOnly_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Expression.Eval("   "));
    }

    [Fact]
    public void Error_UnclosedParenthesis_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Expression.Eval("(1 + 2"));
    }

    [Fact]
    public void Error_ExtraClosingParen_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Expression.Eval("1 + 2)"));
    }

    [Fact]
    public void Error_UnknownFunction_ThrowsFunctionNotFoundException() {
        Assert.Throws<FunctionNotFoundException>(() => Expression.Eval("unknownFunc(1)"));
    }

    [Fact]
    public void Error_FunctionArgCountMismatch_ThrowsFunctionTypeMismatch() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("add", (Func<double, double, double>)((a, b) => a + b));
        Assert.Throws<FunctionTypeMismatchException>(() => Expression.Eval("add(1)", ctx));
    }

    #endregion
}
