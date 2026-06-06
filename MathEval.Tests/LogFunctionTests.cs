using MathEval.Context;
using MathEval.Exceptions;
using MathEval.Fast;
using MathEval.Fast.Exceptions;
using Xunit;

namespace MathEval.Tests;

/// <summary>
/// log 函数单元测试，测试参数数量为 0、1、2、3 的情况
/// </summary>
public class LogFunctionTests {
    private readonly ExpressionContext _ctx = new();

    #region MathEval log 测试

    [Fact]
    public void MathEval_Log_ZeroArgs_ThrowsException() {
        // 0个参数 - 应该抛出异常
        Assert.Throws<FunctionTypeMismatchException>(() => Expression.Eval("log()", _ctx));
    }

    [Fact]
    public void MathEval_Log_OneArg_ReturnsNaturalLog() {
        // 1个参数 - 自然对数 ln(e) = 1
        var result = Expression.Eval<double>("log(E)", _ctx);
        Assert.Equal(1.0, result, 5);
    }

    [Fact]
    public void MathEval_Log_OneArg_ReturnsCorrectValue() {
        // 1个参数 - log(10) ≈ 2.3026
        var result = Expression.Eval<double>("log(10)", _ctx);
        Assert.Equal(Math.Log(10), result, 10);
    }

    [Fact]
    public void MathEval_Log_TwoArgs_ReturnsBaseLog() {
        // 2个参数 - 以b为底的对数 log(100, 10) = 2
        var result = Expression.Eval<double>("log(100, 10)", _ctx);
        Assert.Equal(2.0, result, 10);
    }

    [Fact]
    public void MathEval_Log_TwoArgs_ReturnsCorrectValue() {
        // 2个参数 - log(8, 2) = 3
        var result = Expression.Eval<double>("log(8, 2)", _ctx);
        Assert.Equal(3.0, result, 10);
    }

    [Fact]
    public void MathEval_Log_ThreeArgs_ThrowsException() {
        // 3个参数 - 应该抛出异常
        Assert.Throws<FunctionTypeMismatchException>(() => Expression.Eval("log(100, 10, 1)", _ctx));
    }

    #endregion

    #region MathEval.Fast log 测试

    [Fact]
    public void FastEval_Log_ZeroArgs_ThrowsException() {
        // 0个参数 - 应该抛出 FastEvalException
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("log()"));
    }

    [Fact]
    public void FastEval_Log_OneArg_ReturnsNaturalLog() {
        // 1个参数 - 自然对数 log(e) = 1
        var result = FastEval.EvalDouble("log(2.718281828)");
        Assert.Equal(1.0, result, 5);
    }

    [Fact]
    public void FastEval_Log_OneArg_ReturnsCorrectValue() {
        // 1个参数 - log(10) ≈ 2.3026
        var result = FastEval.EvalDouble("log(10)");
        Assert.Equal(Math.Log(10), result, 10);
    }

    [Fact]
    public void FastEval_Log_TwoArgs_ReturnsBaseLog() {
        // 2个参数 - 以b为底的对数 log(100, 10) = 2
        var result = FastEval.EvalDouble("log(100, 10)");
        Assert.Equal(2.0, result, 10);
    }

    [Fact]
    public void FastEval_Log_TwoArgs_ReturnsCorrectValue() {
        // 2个参数 - log(8, 2) = 3
        var result = FastEval.EvalDouble("log(8, 2)");
        Assert.Equal(3.0, result, 10);
    }

    [Fact]
    public void FastEval_Log_ThreeArgs_ThrowsException() {
        // 3个参数 - 现在应该抛出 FastEvalException
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("log(100, 10, 5)"));
    }

    #endregion

    #region MathEval 与 FastEval 行为对比

    [Fact]
    public void Both_Log_OneArg_SameResult() {
        // 1个参数时，两者应该返回相同结果
        var mathEvalResult = Expression.Eval<double>("log(10)", _ctx);
        var fastEvalResult = FastEval.EvalDouble("log(10)");
        Assert.Equal(mathEvalResult, fastEvalResult, 10);
    }

    [Fact]
    public void Both_Log_TwoArgs_SameResult() {
        // 2个参数时，两者应该返回相同结果
        var mathEvalResult = Expression.Eval<double>("log(100, 10)", _ctx);
        var fastEvalResult = FastEval.EvalDouble("log(100, 10)");
        Assert.Equal(mathEvalResult, fastEvalResult, 10);
    }

    [Fact]
    public void Both_Log_NaturalLog_Equivalent() {
        // log(x) 应该等于 ln(x)
        var logResult = Expression.Eval<double>("log(E)", _ctx);
        var lnResult = Expression.Eval<double>("ln(E)", _ctx);
        Assert.Equal(logResult, lnResult, 10);
    }

    #endregion
}