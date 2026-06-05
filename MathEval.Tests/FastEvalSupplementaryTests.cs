using MathEval.Fast;
using MathEval.Fast.Exceptions;
using Xunit;

namespace MathEval.Tests;

/// <summary>
/// FastEval 补充测试，覆盖 PRD 和 TechDesign_FastEval 中的遗漏场景
/// </summary>
public class FastEvalSupplementaryTests {
    #region 内置函数补充测试

    [Fact]
    public void EvalDouble_TanFunction() {
        Assert.Equal(0.0, FastEval.EvalDouble("tan(0)"), 0.0001);
    }

    [Fact]
    public void EvalDouble_AsinFunction() {
        Assert.Equal(Math.PI / 2, FastEval.EvalDouble("asin(1)"), 0.0001);
    }

    [Fact]
    public void EvalDouble_AcosFunction() {
        Assert.Equal(0.0, FastEval.EvalDouble("acos(1)"), 0.0001);
    }

    [Fact]
    public void EvalDouble_AtanFunction() {
        Assert.Equal(0.0, FastEval.EvalDouble("atan(0)"), 0.0001);
    }

    [Fact]
    public void EvalDouble_Atan2Function() {
        Assert.Equal(0.0, FastEval.EvalDouble("atan2(0, 1)"), 0.0001);
    }

    [Fact]
    public void EvalDouble_ExpFunction() {
        Assert.Equal(Math.E, FastEval.EvalDouble("exp(1)"), 0.0001);
    }

    [Fact]
    public void EvalDouble_Log10Function() {
        Assert.Equal(2.0, FastEval.EvalDouble("log10(100)"), 0.0001);
    }

    [Fact]
    public void EvalDouble_Log2Function() {
        Assert.Equal(3.0, FastEval.EvalDouble("log2(8)"), 0.0001);
    }

    [Fact]
    public void EvalDouble_TruncateFunction() {
        Assert.Equal(3.0, FastEval.EvalDouble("trunc(3.9)"));
    }

    [Fact]
    public void EvalDouble_SignFunction_Positive() {
        Assert.Equal(1.0, FastEval.EvalDouble("sign(5)"));
    }

    [Fact]
    public void EvalDouble_SignFunction_Negative() {
        Assert.Equal(-1.0, FastEval.EvalDouble("sign(-5)"));
    }

    [Fact]
    public void EvalDouble_SignFunction_Zero() {
        Assert.Equal(0.0, FastEval.EvalDouble("sign(0)"));
    }

    [Fact]
    public void EvalDouble_AsinOutOfRange_ReturnsNaN() {
        Assert.True(double.IsNaN(FastEval.EvalDouble("asin(2)")));
    }

    [Fact]
    public void EvalDouble_AcosOutOfRange_ReturnsNaN() {
        Assert.True(double.IsNaN(FastEval.EvalDouble("acos(2)")));
    }

    [Fact]
    public void EvalDouble_Log10NonPositive_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("log10(0)"));
    }

    [Fact]
    public void EvalDouble_Log2NonPositive_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("log2(-1)"));
    }

    [Fact]
    public void EvalLong_SignFunction() {
        Assert.Equal(-1L, FastEval.EvalLong("sign(-10)"));
    }

    #endregion

    #region 特殊值运算

    [Fact]
    public void EvalDouble_NaN_Plus1_ReturnsNaN() {
        Assert.True(double.IsNaN(FastEval.EvalDouble("NaN + 1")));
    }

    [Fact]
    public void EvalDouble_NaN_Times2_ReturnsNaN() {
        Assert.True(double.IsNaN(FastEval.EvalDouble("NaN * 2")));
    }

    [Fact]
    public void EvalDouble_INF_Plus1_ReturnsINF() {
        Assert.True(double.IsPositiveInfinity(FastEval.EvalDouble("INF + 1")));
    }

    [Fact]
    public void EvalDouble_INF_MinusINF_ReturnsNaN() {
        Assert.True(double.IsNaN(FastEval.EvalDouble("INF - INF")));
    }

    [Fact]
    public void EvalDouble_ZeroTimesINF_ReturnsNaN() {
        Assert.True(double.IsNaN(FastEval.EvalDouble("0 * INF")));
    }

    [Fact]
    public void EvalDouble_INF_GreaterThan1_ReturnsTrue() {
        Assert.Equal(1.0, FastEval.EvalDouble("INF > 1"));
    }

    [Fact]
    public void EvalDouble_NaN_EqualNaN_ReturnsFalse() {
        Assert.Equal(0.0, FastEval.EvalDouble("NaN == NaN"));
    }

    [Fact]
    public void EvalDouble_NaN_NotEqualNaN_ReturnsTrue() {
        Assert.Equal(1.0, FastEval.EvalDouble("NaN != NaN"));
    }

    #endregion

    #region 幂运算边界

    [Fact]
    public void EvalDouble_PowerZeroToZero_Returns1() {
        Assert.Equal(1.0, FastEval.EvalDouble("0 ^ 0"));
    }

    [Fact]
    public void EvalDouble_PowerNegativeExponent_Returns0_5() {
        Assert.Equal(0.5, FastEval.EvalDouble("2 ^ -1"));
    }

    [Fact]
    public void EvalDouble_PowerFractionalExponent_Returns3() {
        Assert.Equal(3.0, FastEval.EvalDouble("9 ^ 0.5"));
    }

    [Fact]
    public void EvalDouble_PowerNegativeBaseFractionalExponent_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("(-4) ^ 0.5"));
    }

    [Fact]
    public void EvalDouble_PowerNegativeBaseNegativeExponent() {
        var result = FastEval.EvalDouble("(-2) ^ (-3)");
        Assert.Equal(Math.Pow(-2, -3), result, 12);
    }

    [Fact]
    public void EvalDouble_PowerNegativeBaseNegativeExponent_IntegerResult() {
        var result = FastEval.EvalDouble("(-1) ^ (-2)");
        Assert.Equal(1.0, result, 12);
    }

    #endregion

    #region 整除与取模边界

    [Fact]
    public void EvalLong_NegativeIntegerDivide_TruncatesTowardZero() {
        Assert.Equal(-3L, FastEval.EvalLong("-7 // 2"));
    }

    [Fact]
    public void EvalLong_NegativeModulo_SignFollowsLeftOperand() {
        Assert.Equal(-1L, FastEval.EvalLong("-7 % 3"));
    }

    #endregion

    #region 位运算边界

    [Fact]
    public void EvalLong_ShiftAmountMasking_64Returns0() {
        Assert.Equal(1L, FastEval.EvalLong("1 << 64"));
    }

    [Fact]
    public void EvalLong_ShiftAmountMasking_65Returns2() {
        Assert.Equal(2L, FastEval.EvalLong("1 << 65"));
    }

    [Fact]
    public void EvalLong_NegativeShiftAmount_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalLong("1 << -1"));
    }

    [Fact]
    public void EvalLong_NegativeRightShiftAmount_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalLong("16 >> -1"));
    }

    [Fact]
    public void EvalLong_ArithmeticRightShift_PreservesSign() {
        Assert.Equal(-2L, FastEval.EvalLong("-4 >> 1"));
    }

    #endregion

    #region 多进制混合运算

    [Fact]
    public void EvalLong_MixedRadix_Addition() {
        Assert.Equal(273L, FastEval.EvalLong("0xFF + 0o10 + 0b1010"));
    }

    [Fact]
    public void EvalLong_NegativeHex() {
        Assert.Equal(-255L, FastEval.EvalLong("-0xFF"));
    }

    [Fact]
    public void EvalLong_NegativeBinary() {
        Assert.Equal(-10L, FastEval.EvalLong("-0b1010"));
    }

    [Fact]
    public void EvalLong_NegativeOctal() {
        Assert.Equal(-15L, FastEval.EvalLong("-0o17"));
    }

    [Fact]
    public void EvalDouble_HexBitwiseAnd() {
        Assert.Equal(15.0, FastEval.EvalDouble("0xFF & 0x0F"));
    }

    #endregion

    #region 嵌套三元运算

    [Fact]
    public void EvalDouble_NestedTernary_RightAssociative() {
        Assert.Equal(2.0, FastEval.EvalDouble("true ? false ? 1 : 2 : 3"));
    }

    [Fact]
    public void EvalDouble_NestedTernary_FalseBranch() {
        Assert.Equal(2.0, FastEval.EvalDouble("false ? 1 : true ? 2 : 3"));
    }

    [Fact]
    public void EvalLong_NestedTernary_RightAssociative() {
        Assert.Equal(2L, FastEval.EvalLong("true ? false ? 1 : 2 : 3"));
    }

    #endregion

    #region 除法行为

    [Fact]
    public void EvalDouble_Division_ReturnsDouble() {
        Assert.Equal(3.5, FastEval.EvalDouble("7 / 2"));
    }

    [Fact]
    public void EvalLong_DivisionNotInteger_Throws() {
        // 内部统一 double 运算，7/2=3.5 不是整数，无法转换为 long
        Assert.Throws<FastEvalException>(() => FastEval.EvalLong("7 / 2"));
    }

    #endregion

    #region 布尔值运算

    [Fact]
    public void EvalDouble_BoolPlusInt() {
        Assert.Equal(2.0, FastEval.EvalDouble("true + 1"));
    }

    [Fact]
    public void EvalDouble_BoolAndBool() {
        Assert.Equal(2.0, FastEval.EvalDouble("true + true"));
    }

    [Fact]
    public void EvalLong_BoolBitwiseAnd() {
        Assert.Equal(0L, FastEval.EvalLong("true & 6"));
    }

    #endregion

    #region 函数大小写不敏感

    [Fact]
    public void EvalDouble_FunctionCaseInsensitive_SIN() {
        Assert.Equal(0.0, FastEval.EvalDouble("SIN(0)"), 0.0001);
    }

    [Fact]
    public void EvalDouble_FunctionCaseInsensitive_Sqrt() {
        Assert.Equal(5.0, FastEval.EvalDouble("Sqrt(25)"));
    }

    #endregion

    #region 复杂表达式

    [Fact]
    public void EvalDouble_DeeplyNestedParentheses() {
        Assert.Equal(10.0, FastEval.EvalDouble("((((10))))"));
    }

    [Fact]
    public void EvalDouble_ComplexArithmetic() {
        Assert.Equal(50.0, FastEval.EvalDouble("2 + 3 * 4 ^ 2"));
    }

    [Fact]
    public void EvalDouble_NestedFunctionCalls() {
        Assert.Equal(2.0, FastEval.EvalDouble("sqrt(abs(-4))"));
    }

    [Fact]
    public void EvalDouble_MultipleVariables() {
        var vars = new Dictionary<string, double> { ["a"] = 1.0, ["b"] = 2.0, ["c"] = 3.0 };
        Assert.Equal(6.0, FastEval.EvalDouble("a + b + c", vars));
    }

    #endregion

    #region Eval<T> 泛型补充

    [Fact]
    public void EvalGeneric_Double_WithVariables() {
        var vars = new Dictionary<string, object> { ["x"] = 5.0 };
        Assert.Equal(10.0, FastEval.Eval<double>("x * 2", vars));
    }

    [Fact]
    public void EvalGeneric_Long_WithVariables() {
        var vars = new Dictionary<string, object> { ["x"] = 5L };
        Assert.Equal(10L, FastEval.Eval<long>("x * 2", vars));
    }

    [Fact]
    public void EvalGeneric_Bool_WithComparison() {
        var vars = new Dictionary<string, object> { ["x"] = 10.0 };
        Assert.True(FastEval.Eval<bool>("x > 5", vars));
    }

    #endregion

    #region 错误处理补充

    [Fact]
    public void EvalDouble_NullExpression_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble(null!));
    }

    [Fact]
    public void EvalDouble_UnclosedFunctionCall_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("sin(1"));
    }

    [Fact]
    public void EvalDouble_MissingColonInTernary_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("true ? 1"));
    }

    [Fact]
    public void EvalDouble_InvalidHexDigit_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("0xGH"));
    }

    [Fact]
    public void EvalDouble_InvalidOctalDigit_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("0o89"));
    }

    [Fact]
    public void EvalDouble_InvalidBinaryDigit_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("0b12"));
    }

    [Fact]
    public void EvalDouble_EmptyHexPrefix_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("0x"));
    }

    [Fact]
    public void EvalDouble_EmptyOctalPrefix_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("0o"));
    }

    [Fact]
    public void EvalDouble_EmptyBinaryPrefix_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("0b"));
    }

    #endregion
}
