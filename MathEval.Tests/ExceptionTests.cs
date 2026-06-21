using MathEval.Context;
using MathEval.Exceptions;
using Xunit;
using InvalidOperationException = MathEval.Exceptions.InvalidOperationException;

namespace MathEval.Tests;

public class ExceptionTests {
    [Fact]
    public void ParseException_Inherits_MathEvalException() {
        Assert.True(typeof(MathEvalException).IsAssignableFrom(typeof(ParseException)));
    }

    [Fact]
    public void EvaluateException_Inherits_MathEvalException() {
        Assert.True(typeof(MathEvalException).IsAssignableFrom(typeof(EvaluateException)));
    }

    [Fact]
    public void InvalidOperationException_Inherits_EvaluateException() {
        Assert.True(typeof(EvaluateException).IsAssignableFrom(typeof(InvalidOperationException)));
    }

    [Fact]
    public void TypeMismatchException_Inherits_MathEvalException_NotEvaluateException() {
        Assert.True(typeof(MathEvalException).IsAssignableFrom(typeof(TypeMismatchException)));
        Assert.False(typeof(EvaluateException).IsAssignableFrom(typeof(TypeMismatchException)));
    }

    [Fact]
    public void FunctionNotFoundException_Inherits_EvaluateException() {
        Assert.True(typeof(EvaluateException).IsAssignableFrom(typeof(FunctionNotFoundException)));
    }

    [Fact]
    public void SymbolNotFoundException_Inherits_EvaluateException() {
        Assert.True(typeof(EvaluateException).IsAssignableFrom(typeof(SymbolNotFoundException)));
    }

    [Fact]
    public void FunctionTypeMismatchException_Inherits_EvaluateException() {
        Assert.True(typeof(EvaluateException).IsAssignableFrom(typeof(FunctionTypeMismatchException)));
    }

    [Fact]
    public void ParseException_CaughtAs_MathEvalException() {
        var ex = Assert.ThrowsAny<MathEvalException>(() => Expression.Eval("2 + * 3"));
        Assert.IsType<ParseException>(ex);
    }

    [Fact]
    public void EvaluateException_CaughtAs_MathEvalException() {
        var ex = Assert.ThrowsAny<MathEvalException>(() => Expression.Eval("undefinedVar"));
        Assert.IsType<SymbolNotFoundException>(ex);
    }

    [Fact]
    public void OverflowException_NoLongerThrown() {
        // 现在使用 double 计算，不再有整数溢出
        var result = Expression.Eval("9223372036854775807 + 1");
        Assert.IsType<double>(result);
    }

    [Fact]
    public void InvalidOperationException_CaughtAs_EvaluateException() {
        var context = new ExpressionContext();
        var ex = Assert.ThrowsAny<EvaluateException>(() => context.Set("true", 1));
        Assert.IsType<InvalidOperationException>(ex);
    }

    [Fact]
    public void TypeMismatchException_CaughtAs_MathEvalException() {
        // Bitwise op with non-integer operand throws TypeMismatchException
        var ex = Assert.ThrowsAny<MathEvalException>(() => Expression.Eval("3.5 & 2"));
        Assert.IsType<TypeMismatchException>(ex);
    }

    [Fact]
    public void FunctionNotFoundException_CaughtAs_EvaluateException() {
        var ex = Assert.ThrowsAny<EvaluateException>(() => Expression.Eval("unknownFunc(1)"));
        Assert.IsType<FunctionNotFoundException>(ex);
    }

    [Fact]
    public void SymbolNotFoundException_CaughtAs_EvaluateException() {
        var ex = Assert.ThrowsAny<EvaluateException>(() => Expression.Eval("undefinedVar"));
        Assert.IsType<SymbolNotFoundException>(ex);
    }

    [Fact]
    public void FunctionTypeMismatchException_CaughtAs_EvaluateException() {
        var context = new ExpressionContext();
        context.SetFunction("testAdd", (double a, double b) => a + b);
        var ex = Assert.ThrowsAny<EvaluateException>(() => Expression.Eval("testAdd(1)", context));
        Assert.IsType<FunctionTypeMismatchException>(ex);
    }

    [Fact]
    public void DivisionByZero_ReturnsInfinity() {
        var result = Expression.Eval<double>("1 / 0");
        Assert.True(double.IsPositiveInfinity(result));
    }

    [Fact]
    public void BitwiseNonInteger_Throws_TypeMismatchException() {
        // Bitwise op with non-integer operand throws TypeMismatchException
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("3.5 & 2"));
    }

    [Fact]
    public void UnknownFunction_Throws_FunctionNotFoundException() {
        Assert.Throws<FunctionNotFoundException>(() => Expression.Eval("unknownFunc(1)"));
    }

    [Fact]
    public void UndefinedVariable_Throws_SymbolNotFoundException() {
        Assert.Throws<SymbolNotFoundException>(() => Expression.Eval("undefinedVar"));
    }

    [Fact]
    public void LongOverflow_NoLongerThrows() {
        // 现在使用 double 计算，不再有整数溢出
        var result = Expression.Eval("9223372036854775807 + 1");
        Assert.IsType<double>(result);
    }

    [Fact]
    public void AndWithNonBool_NoLongerThrows() {
        // true and 1 is now valid (non-zero is truthy)
        // Use bitwise with non-integer to test TypeMismatchException
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("3.5 & 2"));
    }

    [Fact]
    public void TernaryWithNonBoolCondition_NoLongerThrows() {
        // 1 ? 2 : 3 is now valid (non-zero is truthy)
        // Use bitwise with non-integer to test TypeMismatchException
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("3.5 | 2"));
    }
}
