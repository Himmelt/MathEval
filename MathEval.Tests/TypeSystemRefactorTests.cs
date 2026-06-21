using MathEval.Context;
using MathEval.Exceptions;
using MathEval.TypeSystem;
using Xunit;

namespace MathEval.Tests;

public class TypeSystemRefactorTests {
    #region 统一数值类型 - bool/long 统一为 double

    [Fact]
    public void True_Keyword_ReturnsOne() {
        Assert.Equal(1.0, Expression.Eval<double>("true"));
    }

    [Fact]
    public void False_Keyword_ReturnsZero() {
        Assert.Equal(0.0, Expression.Eval<double>("false"));
    }

    [Fact]
    public void IntegerLiteral_IsDouble() {
        Assert.Equal(42.0, Expression.Eval<double>("42"));
    }

    [Fact]
    public void FloatLiteral_IsDouble() {
        Assert.Equal(3.14, Expression.Eval<double>("3.14"), 0.001);
    }

    [Fact]
    public void Comparison_ReturnsDouble() {
        Assert.Equal(1.0, Expression.Eval<double>("1 > 0"));
        Assert.Equal(0.0, Expression.Eval<double>("1 < 0"));
    }

    [Fact]
    public void LogicalAnd_ReturnsDouble() {
        Assert.Equal(1.0, Expression.Eval<double>("1 and 1"));
        Assert.Equal(0.0, Expression.Eval<double>("1 and 0"));
    }

    [Fact]
    public void LogicalOr_ReturnsDouble() {
        Assert.Equal(1.0, Expression.Eval<double>("0 or 1"));
        Assert.Equal(0.0, Expression.Eval<double>("0 or 0"));
    }

    [Fact]
    public void NotOperator_ReturnsDouble() {
        Assert.Equal(0.0, Expression.Eval<double>("not 1"));
        Assert.Equal(1.0, Expression.Eval<double>("not 0"));
    }

    [Fact]
    public void Conditional_UsesDoubleTruthiness() {
        Assert.Equal(1.0, Expression.Eval<double>("1 ? 1 : 0"));
        Assert.Equal(0.0, Expression.Eval<double>("0 ? 1 : 0"));
    }

    [Fact]
    public void EvalBool_Compatibility() {
        Assert.True(Expression.Eval<bool>("1 > 0"));
        Assert.False(Expression.Eval<bool>("1 < 0"));
    }

    [Fact]
    public void EvalLong_Compatibility() {
        Assert.Equal(42L, Expression.Eval<long>("42"));
    }

    [Fact]
    public void EvalInt_Compatibility() {
        Assert.Equal(42, Expression.Eval<int>("42"));
    }

    #endregion

    #region 数组常量

    [Fact]
    public void ArrayLiteral_Basic() {
        var result = Expression.Eval<double[]>("[1, 2, 3]");
        Assert.Equal(new double[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void ArrayLiteral_WithExpressions() {
        var result = Expression.Eval<double[]>("[1+2, 3*4, sqrt(9)]");
        Assert.Equal(new double[] { 3, 12, 3 }, result);
    }

    [Fact]
    public void ArrayLiteral_WithVariables() {
        var context = new ExpressionContext();
        context.Set("x", 10.0);
        var result = Expression.Eval<double[]>("[x, x+1, x*2]", context);
        Assert.Equal(new double[] { 10, 11, 20 }, result);
    }

    [Fact]
    public void ArrayLiteral_Empty() {
        var result = Expression.Eval<double[]>("[]");
        Assert.Empty(result);
    }

    [Fact]
    public void ArrayLiteral_SingleElement() {
        var result = Expression.Eval<double[]>("[42]");
        Assert.Equal(new double[] { 42 }, result);
    }

    #endregion

    #region 数组索引

    [Fact]
    public void ArrayIndex_ConstantIndex() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        Assert.Equal(20.0, Expression.Eval<double>("arr[1]", context));
    }

    [Fact]
    public void ArrayIndex_VariableIndex() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        context.Set("i", 2.0);
        Assert.Equal(30.0, Expression.Eval<double>("arr[i]", context));
    }

    [Fact]
    public void ArrayIndex_ExpressionIndex() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30, 40 });
        context.Set("i", 1.0);
        Assert.Equal(40.0, Expression.Eval<double>("arr[i+2]", context));
    }

    [Fact]
    public void ArrayIndex_LiteralWithIndex() {
        Assert.Equal(20.0, Expression.Eval<double>("[10, 20, 30][1]"));
    }

    [Fact]
    public void ArrayIndex_ExpressionWithIndex() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 2, 3 });
        Assert.Equal(6.0, Expression.Eval<double>("(arr * 2)[2]", context));
    }

    [Fact]
    public void ArrayIndex_OutOfBounds_Throws() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 2, 3 });
        Assert.Throws<EvaluateException>(() => Expression.Eval("arr[5]", context));
    }

    [Fact]
    public void ArrayIndex_NegativeIndex_Throws() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 2, 3 });
        Assert.Throws<EvaluateException>(() => Expression.Eval("arr[-1]", context));
    }

    [Fact]
    public void ArrayIndex_ScalarWithIndex_ReturnsScalar() {
        var context = new ExpressionContext();
        context.Set("x", 42.0);
        // Scalar indexed returns the scalar itself (supports index pushdown optimization)
        Assert.Equal(42.0, Expression.Eval<double>("x[0]", context));
    }

    #endregion

    #region 数组逐元素运算

    [Fact]
    public void ArrayPlusScalar() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 2, 3 });
        var result = Expression.Eval<double[]>("arr + 10", context);
        Assert.Equal(new double[] { 11, 12, 13 }, result);
    }

    [Fact]
    public void ScalarPlusArray() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 2, 3 });
        var result = Expression.Eval<double[]>("10 + arr", context);
        Assert.Equal(new double[] { 11, 12, 13 }, result);
    }

    [Fact]
    public void ArrayPlusArray() {
        var context = new ExpressionContext();
        context.Set("a", new double[] { 1, 2, 3 });
        context.Set("b", new double[] { 4, 5, 6 });
        var result = Expression.Eval<double[]>("a + b", context);
        Assert.Equal(new double[] { 5, 7, 9 }, result);
    }

    [Fact]
    public void ArrayMultiplyScalar() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 2, 3 });
        var result = Expression.Eval<double[]>("arr * 2", context);
        Assert.Equal(new double[] { 2, 4, 6 }, result);
    }

    [Fact]
    public void ArrayDivideScalar() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        var result = Expression.Eval<double[]>("arr / 10", context);
        Assert.Equal(new double[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void ArraySubtractArray() {
        var context = new ExpressionContext();
        context.Set("a", new double[] { 10, 20, 30 });
        context.Set("b", new double[] { 1, 2, 3 });
        var result = Expression.Eval<double[]>("a - b", context);
        Assert.Equal(new double[] { 9, 18, 27 }, result);
    }

    [Fact]
    public void ArrayComparison() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 2, 3 });
        var result = Expression.Eval<double[]>("arr > 1.5", context);
        Assert.Equal(new double[] { 0.0, 1.0, 1.0 }, result);
    }

    [Fact]
    public void ArrayUnaryNegate() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, -2, 3 });
        var result = Expression.Eval<double[]>("-arr", context);
        Assert.Equal(new double[] { -1, 2, -3 }, result);
    }

    [Fact]
    public void ArrayUnaryNot() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 0, 1, 0 });
        var result = Expression.Eval<double[]>("not arr", context);
        Assert.Equal(new double[] { 1.0, 0.0, 1.0 }, result);
    }

    [Fact]
    public void ArrayLengthMismatch_Throws() {
        var context = new ExpressionContext();
        context.Set("a", new double[] { 1, 2, 3 });
        context.Set("b", new double[] { 4, 5 });
        Assert.Throws<EvaluateException>(() => Expression.Eval("a + b", context));
    }

    [Fact]
    public void ArrayLiteralArithmetic() {
        var result = Expression.Eval<double[]>("[1, 2, 3] * 2");
        Assert.Equal(new double[] { 2, 4, 6 }, result);
    }

    [Fact]
    public void ArrayLiteralAddArray() {
        var result = Expression.Eval<double[]>("[1, 2, 3] + [4, 5, 6]");
        Assert.Equal(new double[] { 5, 7, 9 }, result);
    }

    #endregion

    #region 函数数组广播

    [Fact]
    public void FunctionArrayBroadcast_Sin() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 0, 1.5707963267948966, 3.141592653589793 }); // 0, pi/2, pi
        var result = Expression.Eval<double[]>("sin(arr)", context);
        Assert.Equal(0.0, result[0], 0.0001);
        Assert.Equal(1.0, result[1], 0.0001);
        Assert.Equal(0.0, result[2], 0.0001);
    }

    [Fact]
    public void FunctionArrayBroadcast_Sqrt() {
        var result = Expression.Eval<double[]>("sqrt([1, 4, 9])");
        Assert.Equal(new double[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void FunctionArrayBroadcast_MultiArg() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 4, 9 });
        // 聚合函数 max 不广播，展平后全局归约 → max(1,4,9,5) = 9
        var result = Expression.Eval<double>("max(arr, 5)", context);
        Assert.Equal(9.0, result);
    }

    [Fact]
    public void FunctionArrayBroadcast_Pow() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 2, 3 });
        var result = Expression.Eval<double[]>("pow(arr, 2)", context);
        Assert.Equal(new double[] { 1, 4, 9 }, result);
    }

    [Fact]
    public void FunctionArrayBroadcast_ArrayArray() {
        var context = new ExpressionContext();
        context.Set("a", new double[] { 2, 3, 4 });
        context.Set("b", new double[] { 1, 2, 3 });
        var result = Expression.Eval<double[]>("pow(a, b)", context);
        Assert.Equal(new double[] { 2, 9, 64 }, result);
    }

    #endregion

    #region 索引下推优化

    [Fact]
    public void IndexPushdown_ArrayMultiplyScalar() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 2, 3, 4, 5 });
        // (arr * 2)[3] should be optimized to arr[3] * 2 = 8
        Assert.Equal(8.0, Expression.Eval<double>("(arr * 2)[3]", context));
    }

    [Fact]
    public void IndexPushdown_ArrayAddScalar() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        Assert.Equal(25.0, Expression.Eval<double>("(arr + 5)[1]", context));
    }

    [Fact]
    public void IndexPushdown_FunctionCall() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 0, 1.5707963267948966, 3.141592653589793 });
        // sin(arr)[1] should be optimized to sin(arr[1]) ≈ 1.0
        Assert.Equal(1.0, Expression.Eval<double>("sin(arr)[1]", context), 0.0001);
    }

    [Fact]
    public void IndexPushdown_ArrayAddArray() {
        var context = new ExpressionContext();
        context.Set("a", new double[] { 1, 2, 3 });
        context.Set("b", new double[] { 4, 5, 6 });
        // (a + b)[1] should be optimized to a[1] + b[1] = 7
        Assert.Equal(7.0, Expression.Eval<double>("(a + b)[1]", context));
    }

    [Fact]
    public void IndexPushdown_UnaryNegate() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 2, 3 });
        Assert.Equal(-2.0, Expression.Eval<double>("(-arr)[1]", context));
    }

    [Fact]
    public void IndexPushdown_ComplexExpression() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 2, 3 });
        context.Set("x", 5.0);
        // (arr * x + 10)[2] → arr[2] * x + 10 = 3*5+10 = 25
        Assert.Equal(25.0, Expression.Eval<double>("(arr * x + 10)[2]", context));
    }

    #endregion

    #region 字符串运算已移除

    [Fact]
    public void StringLiteral_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Expression.Eval("\"hello\""));
    }

    [Fact]
    public void InterpolatedString_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Expression.Eval("$\"hello {1}\""));
    }

    #endregion

    #region 综合场景

    [Fact]
    public void ComplexArrayExpression() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 4, 9, 16, 25 });
        // sqrt(arr) + 1 → [2, 3, 4, 5, 6], then [3] = 5
        Assert.Equal(5.0, Expression.Eval<double>("(sqrt(arr) + 1)[3]", context));
    }

    [Fact]
    public void ArrayElementInArithmetic() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        Assert.Equal(30.0, Expression.Eval<double>("arr[0] + arr[1]", context));
    }

    [Fact]
    public void ArrayElementWithFunction() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 4, 9, 16 });
        Assert.Equal(3.0, Expression.Eval<double>("sqrt(arr[1])", context));
    }

    [Fact]
    public void ArrayWithConditional() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        context.Set("flag", 1.0);
        Assert.Equal(20.0, Expression.Eval<double>("flag ? arr[1] : arr[0]", context));
    }

    [Fact]
    public void EvalDoubleArray_ReturnType() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 2, 3 });
        var result = Expression.Eval<double[]>("arr * 2", context);
        Assert.IsType<double[]>(result);
        Assert.Equal(3, result.Length);
    }

    [Fact]
    public void EvalListOfDouble_ReturnType() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 2, 3 });
        var result = Expression.Eval<List<double>>("arr * 2", context);
        Assert.Equal([2, 4, 6], result);
    }

    [Fact]
    public void PowerOperator() {
        Assert.Equal(8.0, Expression.Eval<double>("2 ** 3"));
    }

    [Fact]
    public void BitwiseAndOperator() {
        // 5 = 101, 3 = 011, 5 & 3 = 001 = 1
        Assert.Equal(1.0, Expression.Eval<double>("5 & 3"));
    }

    [Fact]
    public void BitwiseOrOperator() {
        Assert.Equal(7.0, Expression.Eval<double>("5 | 3")); // 101 | 011 = 111 = 7
    }

    #endregion

    #region TypeHelper.ToInteger 完整测试

    #region 直接调用 - 所有整数类型应成功

    [Fact]
    public void ToInteger_Int_ReturnsLong() {
        Assert.Equal(5L, TypeHelper.ToInteger(5, "op"));
    }

    [Fact]
    public void ToInteger_Long_ReturnsLong() {
        Assert.Equal(5L, TypeHelper.ToInteger(5L, "op"));
    }

    [Fact]
    public void ToInteger_FloatInteger_ReturnsLong() {
        Assert.Equal(5L, TypeHelper.ToInteger(5.0f, "op"));
    }

    [Fact]
    public void ToInteger_DoubleInteger_ReturnsLong() {
        Assert.Equal(5L, TypeHelper.ToInteger(5.0, "op"));
    }

    [Fact]
    public void ToInteger_Short_ReturnsLong() {
        Assert.Equal(5L, TypeHelper.ToInteger((short)5, "op"));
    }

    [Fact]
    public void ToInteger_Byte_ReturnsLong() {
        Assert.Equal(5L, TypeHelper.ToInteger((byte)5, "op"));
    }

    [Fact]
    public void ToInteger_SByte_ReturnsLong() {
        Assert.Equal(5L, TypeHelper.ToInteger((sbyte)5, "op"));
    }

    [Fact]
    public void ToInteger_UShort_ReturnsLong() {
        Assert.Equal(5L, TypeHelper.ToInteger((ushort)5, "op"));
    }

    [Fact]
    public void ToInteger_UInt_ReturnsLong() {
        Assert.Equal(5L, TypeHelper.ToInteger((uint)5, "op"));
    }

    [Fact]
    public void ToInteger_ULong_ReturnsLong() {
        Assert.Equal(5L, TypeHelper.ToInteger((ulong)5, "op"));
    }

    [Fact]
    public void ToInteger_DecimalInteger_ReturnsLong() {
        Assert.Equal(5L, TypeHelper.ToInteger(5m, "op"));
    }

    [Fact]
    public void ToInteger_BoolTrue_ReturnsOne() {
        Assert.Equal(1L, TypeHelper.ToInteger(true, "op"));
    }

    [Fact]
    public void ToInteger_BoolFalse_ReturnsZero() {
        Assert.Equal(0L, TypeHelper.ToInteger(false, "op"));
    }

    [Fact]
    public void ToInteger_Zero_ReturnsZero() {
        Assert.Equal(0L, TypeHelper.ToInteger(0, "op"));
    }

    [Fact]
    public void ToInteger_NegativeInt_ReturnsNegative() {
        Assert.Equal(-5L, TypeHelper.ToInteger(-5, "op"));
    }

    [Fact]
    public void ToInteger_NegativeDouble_ReturnsNegative() {
        Assert.Equal(-5L, TypeHelper.ToInteger(-5.0, "op"));
    }

    #endregion

    #region 直接调用 - 非整数应抛异常

    [Fact]
    public void ToInteger_NonIntegerDouble_Throws() {
        Assert.Throws<TypeMismatchException>(() => TypeHelper.ToInteger(3.5, "按位与"));
    }

    [Fact]
    public void ToInteger_NaN_Throws() {
        Assert.Throws<TypeMismatchException>(() => TypeHelper.ToInteger(double.NaN, "op"));
    }

    [Fact]
    public void ToInteger_PositiveInfinity_Throws() {
        Assert.Throws<TypeMismatchException>(() => TypeHelper.ToInteger(double.PositiveInfinity, "op"));
    }

    [Fact]
    public void ToInteger_NegativeInfinity_Throws() {
        Assert.Throws<TypeMismatchException>(() => TypeHelper.ToInteger(double.NegativeInfinity, "op"));
    }

    [Fact]
    public void ToInteger_String_Throws() {
        Assert.Throws<TypeMismatchException>(() => TypeHelper.ToInteger("abc", "op"));
    }

    [Fact]
    public void ToInteger_Null_Throws() {
        Assert.Throws<TypeMismatchException>(() => TypeHelper.ToInteger(null!, "op"));
    }

    #endregion

    #region 通过表达式 - 整数类型变量参与位运算

    [Fact]
    public void BitwiseAnd_IntVariable_ReturnsCorrectResult() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 5); // int
        Assert.Equal(1.0, Expression.Eval<double>("x & 3", ctx, ExpressionOptions.NoCache));
    }

    [Fact]
    public void BitwiseAnd_LongVariable_ReturnsCorrectResult() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 5L); // long
        Assert.Equal(1.0, Expression.Eval<double>("x & 3", ctx, ExpressionOptions.NoCache));
    }

    [Fact]
    public void BitwiseAnd_FloatIntegerVariable_ReturnsCorrectResult() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 5.0f); // float (整数值)
        Assert.Equal(1.0, Expression.Eval<double>("x & 3", ctx, ExpressionOptions.NoCache));
    }

    [Fact]
    public void BitwiseAnd_ByteVariable_ReturnsCorrectResult() {
        var ctx = new ExpressionContext();
        ctx.Set("x", (byte)5);
        Assert.Equal(1.0, Expression.Eval<double>("x & 3", ctx, ExpressionOptions.NoCache));
    }

    [Fact]
    public void BitwiseAnd_ShortVariable_ReturnsCorrectResult() {
        var ctx = new ExpressionContext();
        ctx.Set("x", (short)5);
        Assert.Equal(1.0, Expression.Eval<double>("x & 3", ctx, ExpressionOptions.NoCache));
    }

    [Fact]
    public void BitwiseAnd_DecimalVariable_ReturnsCorrectResult() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 5m); // decimal
        Assert.Equal(1.0, Expression.Eval<double>("x & 3", ctx, ExpressionOptions.NoCache));
    }

    [Fact]
    public void BitwiseAnd_BoolVariable_ReturnsCorrectResult() {
        var ctx = new ExpressionContext();
        ctx.Set("x", true); // bool → 1
        Assert.Equal(1.0, Expression.Eval<double>("x & 3", ctx, ExpressionOptions.NoCache));
    }

    [Fact]
    public void BitwiseOr_IntVariable_ReturnsCorrectResult() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 5); // int, 101 | 011 = 111 = 7
        Assert.Equal(7.0, Expression.Eval<double>("x | 3", ctx, ExpressionOptions.NoCache));
    }

    [Fact]
    public void BitwiseXor_IntVariable_ReturnsCorrectResult() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 5); // int, 101 xor 011 = 110 = 6
        Assert.Equal(6.0, Expression.Eval<double>("x xor 3", ctx, ExpressionOptions.NoCache));
    }

    [Fact]
    public void BitwiseNot_IntVariable_ReturnsCorrectResult() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 5); // int, ~5 = -6
        Assert.Equal(-6.0, Expression.Eval<double>("~x", ctx, ExpressionOptions.NoCache));
    }

    [Fact]
    public void LeftShift_IntVariable_ReturnsCorrectResult() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 5); // int, 5 << 1 = 10
        Assert.Equal(10.0, Expression.Eval<double>("x << 1", ctx, ExpressionOptions.NoCache));
    }

    [Fact]
    public void RightShift_IntVariable_ReturnsCorrectResult() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 8); // int, 8 >> 1 = 4
        Assert.Equal(4.0, Expression.Eval<double>("x >> 1", ctx, ExpressionOptions.NoCache));
    }

    [Fact]
    public void UnsignedRightShift_IntVariable_ReturnsCorrectResult() {
        var ctx = new ExpressionContext();
        ctx.Set("x", -8); // int → long(64-bit), -8L >>> 1 = 0x7FFFFFFFFFFFFFFC
        Assert.Equal(9223372036854775804.0, Expression.Eval<double>("x >>> 1", ctx, ExpressionOptions.NoCache));
    }

    #endregion

    #region 通过表达式 - 数组索引支持整数类型

    [Fact]
    public void ArrayIndex_IntIndex() {
        var ctx = new ExpressionContext();
        ctx.Set("arr", new double[] { 10, 20, 30 });
        ctx.Set("i", 1); // int
        Assert.Equal(20.0, Expression.Eval<double>("arr[i]", ctx));
    }

    [Fact]
    public void ArrayIndex_LongIndex() {
        var ctx = new ExpressionContext();
        ctx.Set("arr", new double[] { 10, 20, 30 });
        ctx.Set("i", 2L); // long
        Assert.Equal(30.0, Expression.Eval<double>("arr[i]", ctx));
    }

    [Fact]
    public void ArrayIndex_ByteIndex() {
        var ctx = new ExpressionContext();
        ctx.Set("arr", new double[] { 10, 20, 30 });
        ctx.Set("i", (byte)0);
        Assert.Equal(10.0, Expression.Eval<double>("arr[i]", ctx));
    }

    [Fact]
    public void ArrayIndex_ShortIndex() {
        var ctx = new ExpressionContext();
        ctx.Set("arr", new double[] { 10, 20, 30 });
        ctx.Set("i", (short)1);
        Assert.Equal(20.0, Expression.Eval<double>("arr[i]", ctx));
    }

    #endregion

    #region 通过表达式 - 非整数参与位运算应抛异常

    [Fact]
    public void BitwiseAnd_NonIntegerDouble_Throws() {
        Assert.Throws<TypeMismatchException>(() =>
            Expression.Eval<double>("3.5 & 1", null, ExpressionOptions.NoCache));
    }

    [Fact]
    public void BitwiseAnd_NaN_Throws() {
        Assert.Throws<TypeMismatchException>(() =>
            Expression.Eval<double>("NaN & 1", null, ExpressionOptions.NoCache));
    }

    [Fact]
    public void BitwiseAnd_Infinity_Throws() {
        Assert.Throws<TypeMismatchException>(() =>
            Expression.Eval<double>("INF & 1", null, ExpressionOptions.NoCache));
    }

    [Fact]
    public void BitwiseNot_NonIntegerDouble_Throws() {
        Assert.Throws<TypeMismatchException>(() =>
            Expression.Eval<double>("~3.5", null, ExpressionOptions.NoCache));
    }

    #endregion

    #endregion
}