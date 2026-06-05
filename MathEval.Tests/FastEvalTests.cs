using MathEval.Fast;
using MathEval.Fast.Exceptions;
using Xunit;

namespace MathEval.Tests;

public class FastEvalTests {
    #region 基础算术

    [Fact]
    public void EvalDouble_SimpleAddition_Returns7() {
        Assert.Equal(7.0, FastEval.EvalDouble("3 + 4"));
    }

    [Fact]
    public void EvalDouble_MulPriority_Returns14() {
        Assert.Equal(14.0, FastEval.EvalDouble("2 + 3 * 4"));
    }

    [Fact]
    public void EvalDouble_SubLeftAssoc_Returns5() {
        Assert.Equal(5.0, FastEval.EvalDouble("10 - 3 - 2"));
    }

    [Fact]
    public void EvalDouble_DivLeftAssoc_Returns5() {
        Assert.Equal(5.0, FastEval.EvalDouble("100 / 10 / 2"));
    }

    [Fact]
    public void EvalDouble_NegativeNumber_ReturnsNeg3() {
        Assert.Equal(-3.0, FastEval.EvalDouble("-3"));
    }

    [Fact]
    public void EvalDouble_DoubleNegation_Returns3() {
        Assert.Equal(3.0, FastEval.EvalDouble("--3"));
    }

    [Fact]
    public void EvalDouble_Parentheses_Returns14() {
        Assert.Equal(14.0, FastEval.EvalDouble("(2 + 3) * 4 - 6"));
    }

    [Fact]
    public void EvalDouble_FloatLiteral_Returns3_14() {
        Assert.Equal(3.14, FastEval.EvalDouble("3.14"), 0.001);
    }

    [Fact]
    public void EvalDouble_ScientificNotation_Returns1e10() {
        Assert.Equal(1e10, FastEval.EvalDouble("1e10"));
    }

    [Fact]
    public void EvalDouble_LeadingDot_Returns0_5() {
        Assert.Equal(0.5, FastEval.EvalDouble(".5"));
    }

    #endregion

    #region 幂运算

    [Fact]
    public void EvalDouble_PowerBasic_Returns8() {
        Assert.Equal(8.0, FastEval.EvalDouble("2 ^ 3"));
    }

    [Fact]
    public void EvalDouble_PowerRightAssoc_Returns512() {
        Assert.Equal(512.0, FastEval.EvalDouble("2 ^ 3 ^ 2"));
    }

    [Fact]
    public void EvalDouble_PowerFractional_Returns3() {
        Assert.Equal(3.0, FastEval.EvalDouble("9 ^ 0.5"));
    }

    [Fact]
    public void EvalLong_PowerBasic_Returns8() {
        Assert.Equal(8L, FastEval.EvalLong("2 ^ 3"));
    }

    [Fact]
    public void EvalLong_PowerZeroToZero_Returns1() {
        Assert.Equal(1L, FastEval.EvalLong("0 ^ 0"));
    }

    #endregion

    #region 整除与取模

    [Fact]
    public void EvalDouble_IntegerDivide_Returns3() {
        Assert.Equal(3.0, FastEval.EvalDouble("7 // 2"));
    }

    [Fact]
    public void EvalLong_IntegerDivide_Returns3() {
        Assert.Equal(3L, FastEval.EvalLong("7 // 2"));
    }

    [Fact]
    public void EvalDouble_Modulo_Returns1() {
        Assert.Equal(1.0, FastEval.EvalDouble("7 % 2"));
    }

    [Fact]
    public void EvalLong_Modulo_Returns1() {
        Assert.Equal(1L, FastEval.EvalLong("7 % 2"));
    }

    #endregion

    #region 比较运算

    [Fact]
    public void EvalDouble_LessThan() {
        Assert.Equal(1.0, FastEval.EvalDouble("3 < 5"));
        Assert.Equal(0.0, FastEval.EvalDouble("5 < 3"));
    }

    [Fact]
    public void EvalDouble_GreaterThan_True() {
        Assert.Equal(1.0, FastEval.EvalDouble("5 > 3"));
    }

    [Fact]
    public void EvalDouble_Equal_True() {
        Assert.Equal(1.0, FastEval.EvalDouble("3 == 3"));
    }

    [Fact]
    public void EvalDouble_NotEqual_True() {
        Assert.Equal(1.0, FastEval.EvalDouble("3 != 4"));
    }

    [Fact]
    public void EvalDouble_LessOrEqual_True() {
        Assert.Equal(1.0, FastEval.EvalDouble("3 <= 3"));
    }

    [Fact]
    public void EvalDouble_GreaterOrEqual_True() {
        Assert.Equal(1.0, FastEval.EvalDouble("5 >= 5"));
    }

    [Fact]
    public void EvalDouble_NaNComparison() {
        Assert.Equal(0.0, FastEval.EvalDouble("NaN == NaN"));
        Assert.Equal(1.0, FastEval.EvalDouble("NaN != NaN"));
    }

    #endregion

    #region 逻辑运算

    [Fact]
    public void EvalBool_AndTrueTrue_ReturnsTrue() {
        var vars = new Dictionary<string, object> { ["a"] = true, ["b"] = true };
        Assert.True(FastEval.EvalBool("a and b", vars));
    }

    [Fact]
    public void EvalBool_AndTrueFalse_ReturnsFalse() {
        var vars = new Dictionary<string, object> { ["a"] = true, ["b"] = false };
        Assert.False(FastEval.EvalBool("a and b", vars));
    }

    [Fact]
    public void EvalBool_OrFalseTrue_ReturnsTrue() {
        var vars = new Dictionary<string, object> { ["a"] = false, ["b"] = true };
        Assert.True(FastEval.EvalBool("a or b", vars));
    }

    [Fact]
    public void EvalBool_NotTrue_ReturnsFalse() {
        var vars = new Dictionary<string, object> { ["a"] = true };
        Assert.False(FastEval.EvalBool("not a", vars));
    }

    [Fact]
    public void EvalBool_SymbolAnd_ReturnsTrue() {
        var vars = new Dictionary<string, object> { ["a"] = true, ["b"] = true };
        Assert.True(FastEval.EvalBool("a && b", vars));
    }

    [Fact]
    public void EvalBool_SymbolOr_ReturnsTrue() {
        var vars = new Dictionary<string, object> { ["a"] = false, ["b"] = true };
        Assert.True(FastEval.EvalBool("a || b", vars));
    }

    [Fact]
    public void EvalBool_SymbolNot_ReturnsTrue() {
        var vars = new Dictionary<string, object> { ["a"] = false };
        Assert.True(FastEval.EvalBool("!a", vars));
    }

    [Fact]
    public void EvalBool_Comparison() {
        Assert.True(FastEval.EvalBool("3 > 2"));
        Assert.False(FastEval.EvalBool("3 < 2"));
    }

    [Fact]
    public void EvalBool_TrueFalseConstants() {
        Assert.True(FastEval.EvalBool("true"));
        Assert.False(FastEval.EvalBool("false"));
    }

    [Fact]
    public void EvalBool_ShortCircuitOr() {
        var vars = new Dictionary<string, object> { ["a"] = true };
        Assert.True(FastEval.EvalBool("a or b", vars));
        vars["a"] = false;
        Assert.Throws<FastEvalException>(() => FastEval.EvalBool("a or b", vars));
    }

    [Fact]
    public void EvalBool_ShortCircuitAnd() {
        var vars = new Dictionary<string, object> { ["a"] = false };
        Assert.False(FastEval.EvalBool("a and b", vars));
        vars["a"] = true;
        Assert.Throws<FastEvalException>(() => FastEval.EvalBool("a and b", vars));
    }

    #endregion

    #region 三元运算

    [Fact]
    public void EvalDouble_Ternary() {
        Assert.Equal(1.0, FastEval.EvalDouble("3 > 2 ? 1 : 0"));
        Assert.Equal(0.0, FastEval.EvalDouble("3 < 2 ? 1 : 0"));
    }

    [Fact]
    public void EvalLong_TernaryTrueBranch() {
        Assert.Equal(10L, FastEval.EvalLong("5 > 3 ? 10 : 20"));
    }

    [Fact]
    public void EvalBool_TernaryTrueBranch_ShortCircuit() {
        var vars = new Dictionary<string, object> { ["a"] = true };
        Assert.True(FastEval.EvalBool("a ? true : b", vars));
        vars["a"] = false;
        Assert.Throws<FastEvalException>(() => FastEval.EvalBool("a ? true : b", vars));
    }

    [Fact]
    public void EvalBool_TernaryFalseBranch_ShortCircuit() {
        var vars = new Dictionary<string, object> { ["a"] = false };
        Assert.False(FastEval.EvalBool("a ? b : false", vars));
        vars["a"] = true;
        Assert.Throws<FastEvalException>(() => FastEval.EvalBool("a ? b : false", vars));
    }

    #endregion

    #region 位运算

    [Fact]
    public void EvalLong_BitwiseOr_Returns3() {
        Assert.Equal(3L, FastEval.EvalLong("1 | 2"));
    }

    [Fact]
    public void EvalLong_BitwiseAnd_Returns2() {
        Assert.Equal(2L, FastEval.EvalLong("3 & 2"));
    }

    [Fact]
    public void EvalLong_BitwiseXor_Returns1() {
        Assert.Equal(1L, FastEval.EvalLong("3 xor 2"));
    }

    [Fact]
    public void EvalLong_BitwiseNot_ReturnsNeg1() {
        Assert.Equal(~0L, FastEval.EvalLong("~0"));
    }

    [Fact]
    public void EvalLong_LeftShift_Returns8() {
        Assert.Equal(8L, FastEval.EvalLong("1 << 3"));
    }

    [Fact]
    public void EvalLong_RightShift_Returns2() {
        Assert.Equal(2L, FastEval.EvalLong("16 >> 3"));
    }

    #endregion

    #region 变量

    [Fact]
    public void EvalDouble_VariableAddition() {
        var vars = new Dictionary<string, double> { ["x"] = 5.0, ["y"] = 3.0 };
        Assert.Equal(8.0, FastEval.EvalDouble("x + y", vars));
    }

    [Fact]
    public void EvalLong_VariableMultiplication() {
        var vars = new Dictionary<string, double> { ["a"] = 7.0, ["b"] = 6.0 };
        Assert.Equal(42L, FastEval.EvalLong("a * b", vars));
    }

    [Fact]
    public void EvalDouble_UndefinedVariable_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("unknown + 1"));
    }

    #endregion

    #region 内置函数

    [Fact]
    public void EvalDouble_SinFunction() {
        Assert.Equal(0.0, FastEval.EvalDouble("sin(0)"), 0.0001);
    }

    [Fact]
    public void EvalDouble_CosFunction() {
        Assert.Equal(-1.0, FastEval.EvalDouble("cos(PI)"), 0.0001);
    }

    [Fact]
    public void EvalDouble_SqrtFunction() {
        Assert.Equal(5.0, FastEval.EvalDouble("sqrt(25)"));
    }

    [Fact]
    public void EvalDouble_AbsFunction() {
        Assert.Equal(5.0, FastEval.EvalDouble("abs(-5)"));
    }

    [Fact]
    public void EvalDouble_MaxFunction() {
        Assert.Equal(10.0, FastEval.EvalDouble("max(3, 10)"));
    }

    [Fact]
    public void EvalDouble_MinFunction() {
        Assert.Equal(3.0, FastEval.EvalDouble("min(3, 10)"));
    }

    [Fact]
    public void EvalDouble_PowFunction() {
        Assert.Equal(8.0, FastEval.EvalDouble("pow(2, 3)"));
    }

    [Fact]
    public void EvalDouble_CeilFunction() {
        Assert.Equal(4.0, FastEval.EvalDouble("ceil(3.2)"));
    }

    [Fact]
    public void EvalDouble_FloorFunction() {
        Assert.Equal(3.0, FastEval.EvalDouble("floor(3.8)"));
    }

    [Fact]
    public void EvalDouble_RoundFunction1Arg() {
        Assert.Equal(4.0, FastEval.EvalDouble("round(3.5)"));
    }

    [Fact]
    public void EvalDouble_RoundFunction2Arg() {
        Assert.Equal(3.14, FastEval.EvalDouble("round(3.14159, 2)"), 0.001);
    }

    [Fact]
    public void EvalDouble_LogFunction() {
        Assert.Equal(1.0, FastEval.EvalDouble("ln(E)"), 0.0001);
    }

    [Fact]
    public void EvalDouble_UnknownFunction_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("unknown(1)"));
    }

    [Fact]
    public void EvalLong_AbsFunction() {
        Assert.Equal(5L, FastEval.EvalLong("abs(-5)"));
    }

    [Fact]
    public void EvalLong_MaxFunction() {
        Assert.Equal(10L, FastEval.EvalLong("max(3, 10)"));
    }

    [Fact]
    public void EvalLong_MinFunction() {
        Assert.Equal(3L, FastEval.EvalLong("min(3, 10)"));
    }

    #endregion

    #region 常量

    [Fact]
    public void EvalDouble_PI() {
        Assert.Equal(3.14159265358979, FastEval.EvalDouble("PI"), 0.0001);
    }

    [Fact]
    public void EvalDouble_E() {
        Assert.Equal(2.71828182845905, FastEval.EvalDouble("E"), 0.0001);
    }

    [Fact]
    public void EvalDouble_NaN() {
        Assert.True(double.IsNaN(FastEval.EvalDouble("NaN")));
    }

    [Fact]
    public void EvalDouble_INF() {
        Assert.Equal(double.PositiveInfinity, FastEval.EvalDouble("INF"));
    }

    [Fact]
    public void EvalDouble_TrueFalseConstants() {
        Assert.Equal(1.0, FastEval.EvalDouble("true"));
        Assert.Equal(0.0, FastEval.EvalDouble("false"));
    }

    #endregion

    #region 多进制

    [Fact]
    public void EvalLong_HexLiteral() {
        Assert.Equal(255L, FastEval.EvalLong("0xFF"));
    }

    [Fact]
    public void EvalLong_OctalLiteral() {
        Assert.Equal(15L, FastEval.EvalLong("0o17"));
    }

    [Fact]
    public void EvalLong_BinaryLiteral() {
        Assert.Equal(10L, FastEval.EvalLong("0b1010"));
    }

    [Fact]
    public void EvalDouble_HexLiteral() {
        Assert.Equal(255.0, FastEval.EvalDouble("0xFF"));
    }

    [Fact]
    public void EvalDouble_OctalLiteral() {
        Assert.Equal(15.0, FastEval.EvalDouble("0o17"));
    }

    [Fact]
    public void EvalDouble_BinaryLiteral() {
        Assert.Equal(10.0, FastEval.EvalDouble("0b1010"));
    }

    #endregion

    #region Eval<T> 泛型

    [Fact]
    public void EvalGeneric_Double() {
        Assert.Equal(7.0, FastEval.Eval<double>("3 + 4"));
    }

    [Fact]
    public void EvalGeneric_Long() {
        Assert.Equal(7L, FastEval.Eval<long>("3 + 4"));
    }

    [Fact]
    public void EvalGeneric_Bool() {
        Assert.True(FastEval.Eval<bool>("3 > 2"));
    }

    #endregion

    #region 错误处理

    [Fact]
    public void EvalDouble_EmptyExpression_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble(""));
    }

    [Fact]
    public void EvalDouble_WhitespaceOnly_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("   "));
    }

    [Fact]
    public void EvalDouble_UnexpectedChar_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("#"));
    }

    [Fact]
    public void EvalDouble_UnclosedParen_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("(1 + 2"));
    }

    [Fact]
    public void EvalDouble_ExtraContent_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("1 + 2 3"));
    }

    [Fact]
    public void EvalDouble_TooLongExpression_Throws() {
        var longExpr = new string('1', 4097);
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble(longExpr));
    }

    [Fact]
    public void EvalDouble_SqrtNegative_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("sqrt(-1)"));
    }

    [Fact]
    public void EvalDouble_LogNonPositive_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("ln(0)"));
    }

    [Fact]
    public void EvalDouble_NegativePowerFractional_Throws() {
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("(-2) ^ 0.5"));
    }

    #endregion

    #region EvalLong 特化

    [Fact]
    public void EvalLong_SimpleArithmetic() {
        Assert.Equal(42L, FastEval.EvalLong("6 * 7"));
    }

    [Fact]
    public void EvalLong_CheckedOverflow_Throws() {
        // 内部统一 double 运算，超出 long 范围时抛出 FastEvalException
        Assert.Throws<FastEvalException>(() => FastEval.EvalLong("9223372036854775807 + 1"));
    }

    [Fact]
    public void EvalLong_DivisionNotInteger_Throws() {
        // 内部统一 double 运算，7/2=3.5 不是整数，无法转换为 long
        Assert.Throws<FastEvalException>(() => FastEval.EvalLong("7 / 2"));
    }

    [Fact]
    public void EvalLong_FloatLiteralNotInteger_Throws() {
        // 内部统一 double 运算，3.14 不是整数，无法转换为 long
        Assert.Throws<FastEvalException>(() => FastEval.EvalLong("3.14"));
    }

    #endregion

    #region 复合表达式

    [Fact]
    public void EvalDouble_ComplexExpression() {
        Assert.Equal(14.0, FastEval.EvalDouble("2 + 3 * 4"));
    }

    [Fact]
    public void EvalDouble_NestedFunction() {
        Assert.Equal(2.0, FastEval.EvalDouble("sqrt(abs(-4))"));
    }

    [Fact]
    public void EvalDouble_MixedOperators() {
        var vars = new Dictionary<string, double> { ["x"] = 5.0 };
        Assert.Equal(15.0, FastEval.EvalDouble("(x + 2) * 3 - 6", vars));
    }

    [Fact]
    public void EvalDouble_KeywordOrCaseInsensitive() {
        var vars = new Dictionary<string, object> { ["a"] = true, ["b"] = false };
        Assert.True(FastEval.EvalBool("a OR b", vars));
    }

    [Fact]
    public void EvalDouble_KeywordAndCaseInsensitive() {
        var vars = new Dictionary<string, object> { ["a"] = true, ["b"] = true };
        Assert.True(FastEval.EvalBool("a AND b", vars));
    }

    [Fact]
    public void EvalDouble_KeywordNotCaseInsensitive() {
        var vars = new Dictionary<string, object> { ["a"] = false };
        Assert.True(FastEval.EvalBool("NOT a", vars));
    }

    [Fact]
    public void EvalDouble_KeywordXorCaseInsensitive() {
        Assert.Equal(1L, FastEval.EvalLong("3 XOR 2"));
    }

    #endregion
}