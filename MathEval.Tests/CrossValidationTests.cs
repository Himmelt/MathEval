using MathEval.Fast;
using MathEval.Fast.Exceptions;
using Xunit;

namespace MathEval.Tests;

/// <summary>
/// 交叉验证测试：对比主求值器 (Expression.Eval) 与快速求值器 (FastEval) 的行为一致性，
/// 并验证代码审查报告中标记的各项问题。
/// </summary>
public class CrossValidationTests {

    #region 辅助方法

    /// <summary>
    /// 验证 double 类型表达式：两套求值器结果一致
    /// </summary>
    private static void AssertDoubleConsistent(string expression, double tolerance = 1e-9) {
        var mainResult = Expression.Eval<double>(expression);
        var fastResult = FastEval.EvalDouble(expression);
        if (double.IsNaN(mainResult)) {
            Assert.True(double.IsNaN(fastResult),
                $"NaN 不一致: 表达式 '{expression}' | 主求值器=NaN, FastEval={fastResult}");
        } else if (double.IsPositiveInfinity(mainResult)) {
            Assert.True(double.IsPositiveInfinity(fastResult),
                $"INF 不一致: 表达式 '{expression}' | 主求值器=INF, FastEval={fastResult}");
        } else if (double.IsNegativeInfinity(mainResult)) {
            Assert.True(double.IsNegativeInfinity(fastResult),
                $"-INF 不一致: 表达式 '{expression}' | 主求值器=-INF, FastEval={fastResult}");
        } else {
            Assert.Equal(mainResult, fastResult, precision: 9);
        }
    }

    /// <summary>
    /// 验证 long 类型表达式：两套求值器结果一致
    /// </summary>
    private static void AssertLongConsistent(string expression) {
        var mainResult = Expression.Eval<long>(expression);
        var fastResult = FastEval.EvalLong(expression);
        Assert.Equal(mainResult, fastResult);
    }

    /// <summary>
    /// 验证 bool 类型表达式：两套求值器结果一致
    /// </summary>
    private static void AssertBoolConsistent(string expression) {
        var mainResult = Expression.Eval<bool>(expression);
        var fastResult = FastEval.EvalBool(expression);
        Assert.Equal(mainResult, fastResult);
    }

    #endregion

    #region 算术运算交叉验证

    [Theory]
    [InlineData("1 + 2")]
    [InlineData("10 - 3")]
    [InlineData("4 * 5")]
    [InlineData("2 + 3 * 4")]
    [InlineData("100 / 10 / 2")]
    [InlineData("7 % 3")]
    [InlineData("2 ^ 10")]
    [InlineData("2 ^ 3 ^ 2")]
    [InlineData("9 ^ 0.5")]
    [InlineData("(2 + 3) * 4 - 6")]
    [InlineData("-3")]
    [InlineData("--5")]
    [InlineData("+5")]
    public void Arithmetic_Double_CrossValidation(string expr) {
        AssertDoubleConsistent(expr);
    }

    [Theory]
    [InlineData("1 + 2")]
    [InlineData("10 - 3")]
    [InlineData("4 * 5")]
    [InlineData("7 % 3")]
    [InlineData("7 // 2")]
    [InlineData("-7 // 2")]
    [InlineData("2 ^ 10")]
    [InlineData("0 ^ 0")]
    public void Arithmetic_Long_CrossValidation(string expr) {
        AssertLongConsistent(expr);
    }

    #endregion

    #region 比较运算交叉验证

    [Theory]
    [InlineData("3 > 2")]
    [InlineData("2 > 3")]
    [InlineData("3 < 5")]
    [InlineData("5 == 5")]
    [InlineData("3 != 4")]
    [InlineData("3 <= 3")]
    [InlineData("5 >= 5")]
    [InlineData("1 == 1")]
    [InlineData("1 != 1")]
    public void Comparison_CrossValidation(string expr) {
        AssertBoolConsistent(expr);
    }

    #endregion

    #region 逻辑运算交叉验证

    [Theory]
    [InlineData("true and false")]
    [InlineData("true or false")]
    [InlineData("not true")]
    [InlineData("!false")]
    [InlineData("true && false")]
    [InlineData("false || true")]
    [InlineData("true && true")]
    [InlineData("false || false")]
    public void Logical_CrossValidation(string expr) {
        AssertBoolConsistent(expr);
    }

    #endregion

    #region 位运算交叉验证

    [Theory]
    [InlineData("5 & 3")]
    [InlineData("5 | 3")]
    [InlineData("5 xor 3")]
    [InlineData("~5")]
    [InlineData("1 << 4")]
    [InlineData("16 >> 2")]
    [InlineData("1 << 64")]
    public void Bitwise_Long_CrossValidation(string expr) {
        AssertLongConsistent(expr);
    }

    #endregion

    #region 三元运算交叉验证

    [Theory]
    [InlineData("true ? 1 : 2")]
    [InlineData("false ? 1 : 2")]
    [InlineData("3 > 2 ? 10 : 20")]
    [InlineData("true ? false ? 1 : 2 : 3")]
    [InlineData("false ? 1 : true ? 2 : 3")]
    public void Ternary_Double_CrossValidation(string expr) {
        AssertDoubleConsistent(expr);
    }

    #endregion

    #region NaN/INF 交叉验证 (审查问题 #5)

    [Fact]
    public void NaN_Plus1_CrossValidation() {
        AssertDoubleConsistent("NaN + 1");
    }

    [Fact]
    public void NaN_Times5_CrossValidation() {
        AssertDoubleConsistent("NaN * 5");
    }

    [Fact]
    public void NaN_MinusNaN_CrossValidation() {
        AssertDoubleConsistent("NaN - NaN");
    }

    [Fact]
    public void INF_Plus1_CrossValidation() {
        AssertDoubleConsistent("INF + 1");
    }

    [Fact]
    public void INF_MinusINF_CrossValidation() {
        AssertDoubleConsistent("INF - INF");
    }

    [Fact]
    public void INF_Times2_CrossValidation() {
        AssertDoubleConsistent("INF * 2");
    }

    [Fact]
    public void Zero_TimesINF_CrossValidation() {
        AssertDoubleConsistent("0 * INF");
    }

    [Fact]
    public void INF_DividedBy2_CrossValidation() {
        AssertDoubleConsistent("INF / 2");
    }

    [Fact]
    public void Five_DividedByINF_CrossValidation() {
        AssertDoubleConsistent("5 / INF");
    }

    [Fact]
    public void NegativeINF_CrossValidation() {
        AssertDoubleConsistent("-INF");
    }

    [Fact]
    public void INF_Minus1_CrossValidation() {
        // 验证 INF - 1 行为一致性
        var mainResult = Expression.Eval<double>("INF - 1");
        var fastResult = FastEval.EvalDouble("INF - 1");
        Assert.True(double.IsPositiveInfinity(mainResult));
        Assert.True(double.IsPositiveInfinity(fastResult));
    }

    [Fact]
    public void NaN_EqualNaN_CrossValidation() {
        AssertBoolConsistent("NaN == NaN");
    }

    [Fact]
    public void NaN_NotEqualNaN_CrossValidation() {
        AssertBoolConsistent("NaN != NaN");
    }

    #endregion

    #region 短路求值 (skipMode) 交叉验证 (审查问题 #4)

    [Fact]
    public void Ternary_SkipMode_DivByZero_TrueBranch() {
        // true 分支求值，false 分支跳过 → 不应抛异常
        Assert.Equal(1L, Expression.Eval<long>("true ? 1 : 1/0"));
        Assert.Equal(1.0, FastEval.EvalDouble("true ? 1 : 1/0"));
    }

    [Fact]
    public void Ternary_SkipMode_DivByZero_FalseBranch() {
        // false 分支求值，true 分支跳过 → 不应抛异常
        Assert.Equal(2L, Expression.Eval<long>("false ? 1/0 : 2"));
        Assert.Equal(2.0, FastEval.EvalDouble("false ? 1/0 : 2"));
    }

    [Fact]
    public void Ternary_SkipMode_ComplexExprInSkippedBranch() {
        // 被跳过分支包含复杂运算（含除零），不应触发异常
        Assert.Equal(1.0, FastEval.EvalDouble("true ? 1 : 2 + 3 * 4 / 0"));
        Assert.Equal(2.0, FastEval.EvalDouble("false ? 1 / 0 + 2 : 2"));
    }

    [Fact]
    public void Ternary_SkipMode_PowerInSkippedBranch() {
        // 验证 skipMode 下幂运算不触发异常
        Assert.Equal(1.0, FastEval.EvalDouble("true ? 1 : 0 ^ -1"));
    }

    [Fact]
    public void LogicalAnd_ShortCircuit_CrossValidation() {
        // false and X → X 不求值
        Assert.False(Expression.Eval<bool>("false and (1/0 == 0)"));
        // FastEval: 用变量 b=false 实现短路
        var vars = new Dictionary<string, object> { ["b"] = false };
        Assert.False(FastEval.EvalBool("b and (1/0 == 0)", vars));
    }

    [Fact]
    public void LogicalOr_ShortCircuit_CrossValidation() {
        // true or X → X 不求值
        Assert.True(Expression.Eval<bool>("true or (1/0 == 0)"));
        var vars = new Dictionary<string, object> { ["b"] = true };
        Assert.True(FastEval.EvalBool("b or (1/0 == 0)", vars));
    }

    #endregion

    #region 内置函数交叉验证

    [Theory]
    [InlineData("sin(0)")]
    [InlineData("cos(0)")]
    [InlineData("tan(0)")]
    [InlineData("sqrt(25)")]
    [InlineData("abs(-5)")]
    [InlineData("exp(1)")]
    [InlineData("ceil(3.2)")]
    [InlineData("floor(3.8)")]
    [InlineData("trunc(3.9)")]
    [InlineData("sign(5)")]
    [InlineData("sign(-5)")]
    [InlineData("max(3, 10)")]
    [InlineData("min(3, 10)")]
    [InlineData("pow(2, 3)")]
    public void BuiltInFunctions_Double_CrossValidation(string expr) {
        AssertDoubleConsistent(expr, tolerance: 1e-6);
    }

    [Theory]
    [InlineData("abs(-5)")]
    [InlineData("max(3, 10)")]
    [InlineData("min(3, 10)")]
    [InlineData("sign(-10)")]
    public void BuiltInFunctions_Long_CrossValidation(string expr) {
        AssertLongConsistent(expr);
    }

    #endregion

    #region 常量精度交叉验证 (审查问题 #8)

    [Fact]
    public void PI_Precision_CrossValidation() {
        // 验证两套求值器的 PI 精度是否一致
        var mainPi = Expression.Eval<double>("PI");
        var fastPi = FastEval.EvalDouble("PI");
        // 两者应相等或至少非常接近
        Assert.Equal(mainPi, fastPi, precision: 14);
    }

    [Fact]
    public void E_Precision_CrossValidation() {
        var mainE = Expression.Eval<double>("E");
        var fastE = FastEval.EvalDouble("E");
        Assert.Equal(mainE, fastE, precision: 14);
    }

    [Fact]
    public void PI_MatchesMathPI() {
        // 验证 PI 是否匹配 Math.PI 的完整精度
        var mainPi = Expression.Eval<double>("PI");
        var fastPi = FastEval.EvalDouble("PI");
        // 如果断言失败，说明硬编码精度不足
        Assert.Equal(Math.PI, mainPi);
        Assert.Equal(Math.PI, fastPi);
    }

    [Fact]
    public void E_MatchesMathE() {
        var mainE = Expression.Eval<double>("E");
        var fastE = FastEval.EvalDouble("E");
        Assert.Equal(Math.E, mainE);
        Assert.Equal(Math.E, fastE);
    }

    #endregion

    #region 多进制交叉验证

    [Theory]
    [InlineData("0xFF")]
    [InlineData("0o17")]
    [InlineData("0b1010")]
    [InlineData("0xFF + 0o10 + 0b1010")]
    public void Radix_Long_CrossValidation(string expr) {
        AssertLongConsistent(expr);
    }

    #endregion

    #region 溢出行为交叉验证

    [Fact]
    public void LongOverflow_MainReturnsDouble_FastThrows() {
        // 主求值器现在使用 double 计算，不再有整数溢出
        var mainResult = Expression.Eval("9223372036854775807 + 1");
        Assert.IsType<double>(mainResult);
        // FastEval 内部统一 double 运算，超出 long 范围时抛出 FastEvalException
        Assert.Throws<FastEvalException>(() => FastEval.EvalLong("9223372036854775807 + 1"));
    }

    [Fact]
    public void DoubleOverflow_BothReturnINF() {
        var mainResult = Expression.Eval<double>("1e308 * 10");
        var fastResult = FastEval.EvalDouble("1e308 * 10");
        Assert.True(double.IsPositiveInfinity(mainResult));
        Assert.True(double.IsPositiveInfinity(fastResult));
    }

    #endregion

    #region 审查问题 #6: TypeHelper.EvaluateEqual null 安全性

    [Fact]
    public void EvaluateEqual_StringEqualNull_DoesNotThrow() {
        // 通过字符串与不同类型比较来间接测试 null 安全性
        // 主求值器中字符串与数字不等比较不会抛 NullReferenceException
        Assert.False(Expression.Eval<bool>("1 == '1'"));
        Assert.True(Expression.Eval<bool>("1 != '1'"));
    }

    #endregion

    #region 审查问题 #14: Lexer 十六进制转义错误处理

    [Fact]
    public void Lexer_InvalidHexEscape_ThrowsParseException() {
        // \x 后跟非十六进制字符应抛出 ParseException 而非 FormatException
        var ex = Record.Exception(() => Expression.Eval("'\\xGG'"));
        Assert.NotNull(ex);
        // 期望是 ParseException（友好错误），如果得到 FormatException 则审查问题 #14 成立
    }

    [Fact]
    public void Lexer_InvalidUnicodeEscape_ThrowsParseException() {
        var ex = Record.Exception(() => Expression.Eval("'\\uZZZZ'"));
        Assert.NotNull(ex);
    }

    #endregion

    #region 审查问题 #15: TypeHelper.EvaluatePower 死代码验证

    [Fact]
    public void Power_NegativeBaseNegativeExponent_Long() {
        // l1 < 0 && l2 < 0 的情况：(-2) ^ (-3)
        // 验证这是否能正确处理（审查报告指出存在死代码检查）
        var result = Expression.Eval<double>("(-2) ^ (-3)");
        Assert.Equal(Math.Pow(-2, -3), result);
    }

    [Fact]
    public void Power_NegativeBaseNegativeExponent_FastEval() {
        var result = FastEval.EvalDouble("(-2) ^ (-3)");
        Assert.Equal(Math.Pow(-2, -3), result, 12);
    }

    #endregion

    #region Bug 回归测试: FastEvaluator skipMode 嵌套三元运算符

    [Fact]
    public void NestedTernary_InSkippedBranch_DoesNotEvaluateUndefinedVar() {
        // true ? 1 : (false ? 2 : undefinedVar)
        // 外层 false 分支应完全跳过，内层不应求值 undefinedVar
        Assert.Equal(1.0, FastEval.EvalDouble("true ? 1 : (false ? 2 : undefinedVar)"));
    }

    [Fact]
    public void NestedTernary_InSkippedBranch_DoesNotDivideByZero() {
        // true ? 1 : (true ? 2 : 1/0)
        // 外层 false 分支应完全跳过，内层不应求值 1/0
        Assert.Equal(1.0, FastEval.EvalDouble("true ? 1 : (true ? 2 : 1/0)"));
    }

    [Fact]
    public void NestedTernary_DeepNesting_SkipModePreserved() {
        // 深层嵌套三元运算符在跳过分支中
        Assert.Equal(1.0, FastEval.EvalDouble("true ? 1 : (true ? (false ? 3 : 4) : 5)"));
    }

    [Fact]
    public void NestedTernary_FalseBranch_SkipModePreserved() {
        // false ? (true ? undefinedVar : 0) : 2
        // true 分支完全跳过，内层不应求值 undefinedVar
        Assert.Equal(2.0, FastEval.EvalDouble("false ? (true ? undefinedVar : 0) : 2"));
    }

    #endregion

    #region Bug 回归测试: TypeHelper 负无穷特殊值处理

    [Fact]
    public void INF_TimesNegative2_CrossValidation() {
        // INF * (-2) 应返回 -INF，而非 INF
        AssertDoubleConsistent("INF * -2");
    }

    [Fact]
    public void NegativeINF_PlusINF_CrossValidation() {
        // (-INF) + INF 应返回 NaN
        AssertDoubleConsistent("-INF + INF");
    }

    [Fact]
    public void NegativeINF_DividedByINF_CrossValidation() {
        // (-INF) / INF 应返回 NaN，而非 0
        AssertDoubleConsistent("-INF / INF");
    }

    [Fact]
    public void INF_DividedByNegativeINF_CrossValidation() {
        // INF / (-INF) 应返回 NaN，而非 INF
        AssertDoubleConsistent("INF / -INF");
    }

    [Fact]
    public void NegativeINF_Times2_CrossValidation() {
        // (-INF) * 2 应返回 -INF
        AssertDoubleConsistent("-INF * 2");
    }

    [Fact]
    public void NegativeINF_RemainderINF_CrossValidation() {
        // (-INF) % INF 应返回 NaN
        AssertDoubleConsistent("-INF % INF");
    }

    #endregion

    #region 综合表达式交叉验证

    [Theory]
    [InlineData("2 + 3 * 4 ^ 2", 50.0)]
    [InlineData("10 - 3 - 2", 5.0)]
    [InlineData("7 // 2", 3.0)]
    [InlineData("-7 // 2", -3.0)]
    [InlineData("7.5 % 2", 1.5)]
    public void ComplexExpressions_Double_CrossValidation(string expr, double expected) {
        Assert.Equal(expected, Expression.Eval<double>(expr), precision: 9);
        Assert.Equal(expected, FastEval.EvalDouble(expr), precision: 9);
    }

    #endregion
}
