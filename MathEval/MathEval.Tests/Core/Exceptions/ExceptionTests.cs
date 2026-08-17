using MathEval.Context;
using MathEval.Exceptions;
using Xunit;

namespace MathEval.Tests.Exceptions;

public class ExceptionTests {
    // ---- 层次结构（L0 表达式契约层） ----

    [Fact]
    public void SyntaxException_Inherits_MathEvalException() {
        Assert.True(typeof(MathEvalException).IsAssignableFrom(typeof(SyntaxException)));
    }

    [Fact]
    public void EvaluationException_Inherits_MathEvalException() {
        Assert.True(typeof(MathEvalException).IsAssignableFrom(typeof(EvaluationException)));
    }

    [Fact]
    public void ResolutionException_Inherits_MathEvalException() {
        Assert.True(typeof(MathEvalException).IsAssignableFrom(typeof(ResolutionException)));
    }

    [Fact]
    public void SymbolNotFoundException_Inherits_ResolutionException() {
        Assert.True(typeof(ResolutionException).IsAssignableFrom(typeof(SymbolNotFoundException)));
    }

    [Fact]
    public void FunctionNotFoundException_Inherits_ResolutionException() {
        Assert.True(typeof(ResolutionException).IsAssignableFrom(typeof(FunctionNotFoundException)));
    }

    [Fact]
    public void TypeMismatchException_Inherits_MathEvalException_NotEvaluationException() {
        Assert.True(typeof(MathEvalException).IsAssignableFrom(typeof(TypeMismatchException)));
        Assert.False(typeof(EvaluationException).IsAssignableFrom(typeof(TypeMismatchException)));
    }

    [Fact]
    public void FunctionArgumentTypeException_Inherits_TypeMismatchException() {
        // 函数参数类型错误可被 catch (TypeMismatchException) 统一捕获
        Assert.True(typeof(TypeMismatchException).IsAssignableFrom(typeof(FunctionArgumentTypeException)));
    }

    [Fact]
    public void FunctionArityException_Inherits_EvaluationException() {
        Assert.True(typeof(EvaluationException).IsAssignableFrom(typeof(FunctionArityException)));
    }

    [Fact]
    public void FunctionInvocationException_Inherits_EvaluationException() {
        Assert.True(typeof(EvaluationException).IsAssignableFrom(typeof(FunctionInvocationException)));
    }

    // ---- 行为：异常类型与捕获层级 ----

    [Fact]
    public void SyntaxError_Throws_SyntaxException() {
        var ex = Assert.ThrowsAny<MathEvalException>(() => Expression.Eval("2 + * 3"));
        Assert.IsType<SyntaxException>(ex);
    }

    [Fact]
    public void UndefinedSymbol_Throws_SymbolNotFoundException() {
        var ex = Assert.ThrowsAny<MathEvalException>(() => Expression.Eval("undefinedVar"));
        Assert.IsType<SymbolNotFoundException>(ex);
    }

    [Fact]
    public void UnknownFunction_Throws_FunctionNotFoundException() {
        var ex = Assert.ThrowsAny<MathEvalException>(() => Expression.Eval("unknownFunc(1)"));
        Assert.IsType<FunctionNotFoundException>(ex);
    }

    [Fact]
    public void BitwiseNonInteger_Throws_TypeMismatchException() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("3.5 & 2"));
    }

    [Fact]
    public void FunctionArgCountMismatch_Throws_FunctionArityException() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("doubleIt", (Func<double, double>)(x => x * 2));
        var ex = Assert.ThrowsAny<EvaluationException>(() => Expression.Eval("doubleIt(1, 2)", ctx));
        Assert.IsType<FunctionArityException>(ex);
    }

    [Fact]
    public void FunctionArgTypeMismatch_CaughtAs_TypeMismatchException() {
        var ctx = new ExpressionContext();
        ctx.SetFunction<Guid, string>("badFunc", x => x.ToString());
        // 函数参数类型错误现在属于 TypeMismatchException 家族
        var ex = Assert.ThrowsAny<TypeMismatchException>(() => Expression.Eval("badFunc(1)", ctx));
        Assert.IsType<FunctionArgumentTypeException>(ex);
    }

    [Fact]
    public void UserFunctionThrowing_Throws_FunctionInvocationException() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("boom", (Delegate)(Func<double, double>)(_ => throw new InvalidOperationException("boom")));
        var ex = Assert.ThrowsAny<MathEvalException>(() => Expression.Eval("boom(1)", ctx));
        Assert.IsType<FunctionInvocationException>(ex);
        Assert.NotNull(ex.InnerException);
    }

    // ---- L1 API 误用：不继承 MathEvalException ----

    [Fact]
    public void ReservedKeyword_Throws_ArgumentException_Not_MathEvalException() {
        var ctx = new ExpressionContext();
        Exception ex = Assert.Throws<ArgumentException>(() => ctx.Set("true", 1));
        Assert.False(ex is MathEvalException);
    }

    // ---- 元数据：Position 与 Code ----

    [Fact]
    public void SyntaxException_Carries_CharOffsetPosition() {
        var ex = Assert.Throws<SyntaxException>(() => Expression.Eval("2 + * 3"));
        Assert.True(ex.Position >= 0);
        Assert.Equal(MathEvalErrorCode.UnexpectedToken, ex.Code);
    }

    [Fact]
    public void ResolutionException_Carries_UnknownPosition() {
        var ex = Assert.Throws<SymbolNotFoundException>(() => Expression.Eval("undefinedVar"));
        Assert.Equal(-1, ex.Position);
        Assert.Equal(MathEvalErrorCode.SymbolNotFound, ex.Code);
        Assert.Equal("undefinedVar", ex.Name);
    }
}
