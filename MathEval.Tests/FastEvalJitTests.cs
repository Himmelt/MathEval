using MathEval.Fast;
using MathEval.Fast.Exceptions;
using Xunit;

namespace MathEval.Tests;

/// <summary>
/// JIT 编译运行时测试，验证 Compile / CompileCached 产生的委托执行结果正确
/// </summary>
public class FastEvalJitTests {

    #region Compile 基础算术

    [Fact]
    public void Compile_SimpleAddition_Returns7() {
        var fn = FastEval.Compile("3 + 4");
        Assert.Equal(7.0, fn(null));
    }

    [Fact]
    public void Compile_MulPriority_Returns14() {
        var fn = FastEval.Compile("2 + 3 * 4");
        Assert.Equal(14.0, fn(null));
    }

    [Fact]
    public void Compile_Parentheses_Returns14() {
        var fn = FastEval.Compile("(2 + 3) * 4 - 6");
        Assert.Equal(14.0, fn(null));
    }

    [Fact]
    public void Compile_SubLeftAssoc_Returns5() {
        var fn = FastEval.Compile("10 - 3 - 2");
        Assert.Equal(5.0, fn(null));
    }

    [Fact]
    public void Compile_NegativeNumber_ReturnsNeg3() {
        var fn = FastEval.Compile("-3");
        Assert.Equal(-3.0, fn(null));
    }

    [Fact]
    public void Compile_DoubleNegation_Returns3() {
        var fn = FastEval.Compile("--3");
        Assert.Equal(3.0, fn(null));
    }

    #endregion

    #region Compile 幂运算

    [Fact]
    public void Compile_PowerBasic_Returns8() {
        var fn = FastEval.Compile("2 ^ 3");
        Assert.Equal(8.0, fn(null));
    }

    [Fact]
    public void Compile_PowerRightAssoc_Returns512() {
        var fn = FastEval.Compile("2 ^ 3 ^ 2");
        Assert.Equal(512.0, fn(null));
    }

    #endregion

    #region Compile 整除与取模

    [Fact]
    public void Compile_IntegerDivide_Returns3() {
        var fn = FastEval.Compile("7 // 2");
        Assert.Equal(3.0, fn(null));
    }

    [Fact]
    public void Compile_Modulo_Returns1() {
        var fn = FastEval.Compile("7 % 2");
        Assert.Equal(1.0, fn(null));
    }

    #endregion

    #region Compile 比较运算

    [Fact]
    public void Compile_LessThan() {
        var fn = FastEval.Compile("3 < 5");
        Assert.Equal(1.0, fn(null));
    }

    [Fact]
    public void Compile_GreaterThan() {
        var fn = FastEval.Compile("5 > 3");
        Assert.Equal(1.0, fn(null));
    }

    [Fact]
    public void Compile_Equal() {
        var fn = FastEval.Compile("3 == 3");
        Assert.Equal(1.0, fn(null));
    }

    [Fact]
    public void Compile_NotEqual() {
        var fn = FastEval.Compile("3 != 4");
        Assert.Equal(1.0, fn(null));
    }

    [Fact]
    public void Compile_LessOrEqual() {
        var fn = FastEval.Compile("3 <= 3");
        Assert.Equal(1.0, fn(null));
    }

    [Fact]
    public void Compile_GreaterOrEqual() {
        var fn = FastEval.Compile("5 >= 5");
        Assert.Equal(1.0, fn(null));
    }

    [Fact]
    public void Compile_NaNComparison() {
        var fn = FastEval.Compile("NaN == NaN");
        Assert.Equal(0.0, fn(null));
    }

    #endregion

    #region Compile 逻辑运算

    [Fact]
    public void Compile_LogicalAnd_TrueTrue() {
        var vars = new Dictionary<string, double> { ["a"] = 1.0, ["b"] = 1.0 };
        var fn = FastEval.Compile("a and b");
        Assert.Equal(1.0, fn(vars));
    }

    [Fact]
    public void Compile_LogicalAnd_TrueFalse() {
        var vars = new Dictionary<string, double> { ["a"] = 1.0, ["b"] = 0.0 };
        var fn = FastEval.Compile("a and b");
        Assert.Equal(0.0, fn(vars));
    }

    [Fact]
    public void Compile_LogicalOr_FalseTrue() {
        var vars = new Dictionary<string, double> { ["a"] = 0.0, ["b"] = 1.0 };
        var fn = FastEval.Compile("a or b");
        Assert.Equal(1.0, fn(vars));
    }

    [Fact]
    public void Compile_LogicalNot() {
        var vars = new Dictionary<string, double> { ["a"] = 1.0 };
        var fn = FastEval.Compile("not a");
        Assert.Equal(0.0, fn(vars));
    }

    [Fact]
    public void Compile_SymbolAnd() {
        var vars = new Dictionary<string, double> { ["a"] = 1.0, ["b"] = 1.0 };
        var fn = FastEval.Compile("a && b");
        Assert.Equal(1.0, fn(vars));
    }

    [Fact]
    public void Compile_SymbolOr() {
        var vars = new Dictionary<string, double> { ["a"] = 0.0, ["b"] = 1.0 };
        var fn = FastEval.Compile("a || b");
        Assert.Equal(1.0, fn(vars));
    }

    [Fact]
    public void Compile_SymbolNot() {
        var vars = new Dictionary<string, double> { ["a"] = 0.0 };
        var fn = FastEval.Compile("!a");
        Assert.Equal(1.0, fn(vars));
    }

    #endregion

    #region Compile 三元运算

    [Fact]
    public void Compile_Ternary_TrueBranch() {
        var fn = FastEval.Compile("3 > 2 ? 1 : 0");
        Assert.Equal(1.0, fn(null));
    }

    [Fact]
    public void Compile_Ternary_FalseBranch() {
        var fn = FastEval.Compile("3 < 2 ? 1 : 0");
        Assert.Equal(0.0, fn(null));
    }

    [Fact]
    public void Compile_Ternary_ShortCircuit_TrueBranch() {
        // a=true: should not evaluate b
        var vars = new Dictionary<string, double> { ["a"] = 1.0 };
        var fn = FastEval.Compile("a ? 1 : b");
        Assert.Equal(1.0, fn(vars));
    }

    [Fact]
    public void Compile_Ternary_ShortCircuit_FalseBranch() {
        // a=false: should not evaluate b
        var vars = new Dictionary<string, double> { ["a"] = 0.0 };
        var fn = FastEval.Compile("a ? b : 0");
        Assert.Equal(0.0, fn(vars));
    }

    #endregion

    #region Compile 位运算

    [Fact]
    public void Compile_BitwiseOr() {
        var fn = FastEval.Compile("1 | 2");
        Assert.Equal(3.0, fn(null));
    }

    [Fact]
    public void Compile_BitwiseAnd() {
        var fn = FastEval.Compile("3 & 2");
        Assert.Equal(2.0, fn(null));
    }

    [Fact]
    public void Compile_BitwiseXor() {
        var fn = FastEval.Compile("3 xor 2");
        Assert.Equal(1.0, fn(null));
    }

    [Fact]
    public void Compile_BitwiseNot() {
        var fn = FastEval.Compile("~0");
        Assert.Equal((double)(~0L), fn(null));
    }

    [Fact]
    public void Compile_LeftShift() {
        var fn = FastEval.Compile("1 << 3");
        Assert.Equal(8.0, fn(null));
    }

    [Fact]
    public void Compile_RightShift() {
        var fn = FastEval.Compile("16 >> 3");
        Assert.Equal(2.0, fn(null));
    }

    #endregion

    #region Compile 变量

    [Fact]
    public void Compile_VariableAddition() {
        var vars = new Dictionary<string, double> { ["x"] = 5.0, ["y"] = 3.0 };
        var fn = FastEval.Compile("x + y");
        Assert.Equal(8.0, fn(vars));
    }

    [Fact]
    public void Compile_VariableMultiplication() {
        var vars = new Dictionary<string, double> { ["a"] = 7.0, ["b"] = 6.0 };
        var fn = FastEval.Compile("a * b");
        Assert.Equal(42.0, fn(vars));
    }

    [Fact]
    public void Compile_UndefinedVariable_Throws() {
        var fn = FastEval.Compile("unknown + 1");
        Assert.Throws<FastEvalException>(() => fn(null));
    }

    [Fact]
    public void Compile_NullVariables_ThrowsForVarExpression() {
        var fn = FastEval.Compile("x + 1");
        Assert.Throws<FastEvalException>(() => fn(null));
    }

    #endregion

    #region Compile 内置函数

    [Fact]
    public void Compile_SinFunction() {
        var fn = FastEval.Compile("sin(0)");
        Assert.Equal(0.0, fn(null), 0.0001);
    }

    [Fact]
    public void Compile_CosFunction() {
        var fn = FastEval.Compile("cos(PI)");
        Assert.Equal(-1.0, fn(null), 0.0001);
    }

    [Fact]
    public void Compile_SqrtFunction() {
        var fn = FastEval.Compile("sqrt(25)");
        Assert.Equal(5.0, fn(null));
    }

    [Fact]
    public void Compile_AbsFunction() {
        var fn = FastEval.Compile("abs(-5)");
        Assert.Equal(5.0, fn(null));
    }

    [Fact]
    public void Compile_MaxFunction() {
        var fn = FastEval.Compile("max(3, 10)");
        Assert.Equal(10.0, fn(null));
    }

    [Fact]
    public void Compile_MinFunction() {
        var fn = FastEval.Compile("min(3, 10)");
        Assert.Equal(3.0, fn(null));
    }

    [Fact]
    public void Compile_PowFunction() {
        var fn = FastEval.Compile("pow(2, 3)");
        Assert.Equal(8.0, fn(null));
    }

    [Fact]
    public void Compile_CeilFunction() {
        var fn = FastEval.Compile("ceil(3.2)");
        Assert.Equal(4.0, fn(null));
    }

    [Fact]
    public void Compile_FloorFunction() {
        var fn = FastEval.Compile("floor(3.8)");
        Assert.Equal(3.0, fn(null));
    }

    [Fact]
    public void Compile_Round1Arg() {
        var fn = FastEval.Compile("round(3.5)");
        Assert.Equal(4.0, fn(null));
    }

    [Fact]
    public void Compile_Round2Arg() {
        var fn = FastEval.Compile("round(3.14159, 2)");
        Assert.Equal(3.14, fn(null), 0.001);
    }

    [Fact]
    public void Compile_LnFunction() {
        var fn = FastEval.Compile("ln(E)");
        Assert.Equal(1.0, fn(null), 0.0001);
    }

    [Fact]
    public void Compile_NestedFunction() {
        var fn = FastEval.Compile("sqrt(abs(-4))");
        Assert.Equal(2.0, fn(null));
    }

    #endregion

    #region Compile 常量

    [Fact]
    public void Compile_PI() {
        var fn = FastEval.Compile("PI");
        Assert.Equal(3.14159265358979, fn(null), 0.0001);
    }

    [Fact]
    public void Compile_E() {
        var fn = FastEval.Compile("E");
        Assert.Equal(2.71828182845905, fn(null), 0.0001);
    }

    [Fact]
    public void Compile_NaN() {
        var fn = FastEval.Compile("NaN");
        Assert.True(double.IsNaN(fn(null)));
    }

    [Fact]
    public void Compile_INF() {
        var fn = FastEval.Compile("INF");
        Assert.True(double.IsPositiveInfinity(fn(null)));
    }

    [Fact]
    public void Compile_TrueFalse() {
        var fnTrue = FastEval.Compile("true");
        var fnFalse = FastEval.Compile("false");
        Assert.Equal(1.0, fnTrue(null));
        Assert.Equal(0.0, fnFalse(null));
    }

    #endregion

    #region Compile 多进制

    [Fact]
    public void Compile_HexLiteral() {
        var fn = FastEval.Compile("0xFF");
        Assert.Equal(255.0, fn(null));
    }

    [Fact]
    public void Compile_OctalLiteral() {
        var fn = FastEval.Compile("0o17");
        Assert.Equal(15.0, fn(null));
    }

    [Fact]
    public void Compile_BinaryLiteral() {
        var fn = FastEval.Compile("0b1010");
        Assert.Equal(10.0, fn(null));
    }

    #endregion

    #region Compile 复合表达式

    [Fact]
    public void Compile_ComplexArithmetic() {
        var fn = FastEval.Compile("2 + 3 * 4 ^ 2");
        Assert.Equal(50.0, fn(null));
    }

    [Fact]
    public void Compile_MixedWithVariables() {
        var vars = new Dictionary<string, double> { ["x"] = 5.0 };
        var fn = FastEval.Compile("(x + 2) * 3 - 6");
        Assert.Equal(15.0, fn(vars));
    }

    [Fact]
    public void Compile_DeeplyNestedParentheses() {
        var fn = FastEval.Compile("((((10))))");
        Assert.Equal(10.0, fn(null));
    }

    #endregion

    #region CompileCached

    [Fact]
    public void CompileCached_SimpleExpression() {
        var fn = FastEval.CompileCached("3 + 4");
        Assert.Equal(7.0, fn(null));
    }

    [Fact]
    public void CompileCached_SameExpressionReturnsSameDelegate() {
        var fn1 = FastEval.CompileCached("2 + 3");
        var fn2 = FastEval.CompileCached("2 + 3");
        Assert.Same(fn1, fn2);
    }

    [Fact]
    public void CompileCached_WithVariables() {
        var vars = new Dictionary<string, double> { ["x"] = 10.0 };
        var fn = FastEval.CompileCached("x * 2 + 1");
        Assert.Equal(21.0, fn(vars));
    }

    [Fact]
    public void CompileCached_MultipleExpressions() {
        var fn1 = FastEval.CompileCached("1 + 2");
        var fn2 = FastEval.CompileCached("3 * 4");
        Assert.Equal(3.0, fn1(null));
        Assert.Equal(12.0, fn2(null));
    }

    #endregion

    #region EvalDoubleCached 与 Compile 结果一致性

    [Theory]
    [InlineData("3 + 4", 7.0)]
    [InlineData("2 * 5 - 3", 7.0)]
    [InlineData("10 / 2", 5.0)]
    [InlineData("2 ^ 10", 1024.0)]
    [InlineData("7 // 2", 3.0)]
    [InlineData("7 % 3", 1.0)]
    [InlineData("3 < 5", 1.0)]
    [InlineData("5 > 3", 1.0)]
    [InlineData("3 == 3", 1.0)]
    [InlineData("3 != 4", 1.0)]
    [InlineData("sqrt(144)", 12.0)]
    [InlineData("abs(-42)", 42.0)]
    [InlineData("PI", 3.14159265358979)]
    [InlineData("0xFF", 255.0)]
    public void Compile_MatchesEvalDouble(string expression, double expected) {
        var direct = FastEval.EvalDouble(expression);
        var cached = FastEval.EvalDoubleCached(expression);
        var compiled = FastEval.Compile(expression)(null);

        Assert.Equal(expected, direct, 0.0001);
        Assert.Equal(expected, cached, 0.0001);
        Assert.Equal(expected, compiled, 0.0001);
    }

    #endregion

    #region 重复调用验证

    [Fact]
    public void Compile_MultipleCallsWithDifferentVariables() {
        var fn = FastEval.Compile("x * 2 + y");
        var vars1 = new Dictionary<string, double> { ["x"] = 3.0, ["y"] = 1.0 };
        var vars2 = new Dictionary<string, double> { ["x"] = 10.0, ["y"] = 5.0 };

        Assert.Equal(7.0, fn(vars1));
        Assert.Equal(25.0, fn(vars2));
        Assert.Equal(7.0, fn(vars1)); // 再次验证，确保无状态泄漏
    }

    [Fact]
    public void CompileCached_MultipleCallsWithDifferentVariables() {
        var fn = FastEval.CompileCached("a + b * c");
        var vars1 = new Dictionary<string, double> { ["a"] = 1.0, ["b"] = 2.0, ["c"] = 3.0 };
        var vars2 = new Dictionary<string, double> { ["a"] = 10.0, ["b"] = 5.0, ["c"] = 2.0 };

        Assert.Equal(7.0, fn(vars1));
        Assert.Equal(20.0, fn(vars2));
    }

    #endregion
}
