using MathEval.Context;
using MathEval.Exceptions;
using Xunit;

namespace MathEval.Tests;

public class ArrayIndexTests {
    #region 常量索引

    [Fact]
    public void ArrayIndex_ConstantIndex_FirstElement() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        Assert.Equal(10.0, Expression.Eval<double>("arr[0]", context));
    }

    [Fact]
    public void ArrayIndex_ConstantIndex_MiddleElement() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        Assert.Equal(20.0, Expression.Eval<double>("arr[1]", context));
    }

    [Fact]
    public void ArrayIndex_ConstantIndex_LastElement() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        Assert.Equal(30.0, Expression.Eval<double>("arr[2]", context));
    }

    #endregion

    #region 变量索引

    [Fact]
    public void ArrayIndex_VariableIndex_BasicAccess() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        context.Set("i", 2);
        Assert.Equal(30.0, Expression.Eval<double>("arr[i]", context));
    }

    [Fact]
    public void ArrayIndex_VariableIndex_ZeroIndex() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        context.Set("i", 0);
        Assert.Equal(10.0, Expression.Eval<double>("arr[i]", context));
    }

    #endregion

    #region 表达式索引

    [Fact]
    public void ArrayIndex_ExpressionIndex_Addition() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        context.Set("i", 1);
        Assert.Equal(30.0, Expression.Eval<double>("arr[i+1]", context));
    }

    [Fact]
    public void ArrayIndex_ExpressionIndex_Arithmetic() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30, 40, 50 });
        context.Set("i", 1);
        Assert.Equal(40.0, Expression.Eval<double>("arr[i*2+1]", context));
    }

    [Fact]
    public void ArrayIndex_ExpressionIndex_PureConstant() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        Assert.Equal(20.0, Expression.Eval<double>("arr[1+0]", context));
    }

    #endregion

    #region 数组元素参与运算

    [Fact]
    public void ArrayIndex_ElementAddition() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        Assert.Equal(30.0, Expression.Eval<double>("arr[0] + arr[1]", context));
    }

    [Fact]
    public void ArrayIndex_ElementMultiplication() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 2, 3, 4 });
        Assert.Equal(6.0, Expression.Eval<double>("arr[0] * arr[1]", context));
    }

    [Fact]
    public void ArrayIndex_ElementWithScalar() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        context.Set("x", 5);
        Assert.Equal(15.0, Expression.Eval<double>("arr[0] + x", context));
    }

    [Fact]
    public void ArrayIndex_ComplexExpression() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 2, 3, 4 });
        context.Set("i", 1);
        Assert.Equal(10.0, Expression.Eval<double>("arr[0] * arr[i] + arr[2]", context));
    }

    [Fact]
    public void ArrayIndex_WithFunctionCall() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 4, 9, 16 });
        Assert.Equal(3.0, Expression.Eval<double>("sqrt(arr[1])", context));
    }

    [Fact]
    public void ArrayIndex_WithConditional() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        context.Set("flag", true);
        Assert.Equal(20.0, Expression.Eval<double>("flag ? arr[1] : arr[0]", context));
    }

    #endregion

    #region 不同数组类型

    [Fact]
    public void ArrayIndex_DoubleArray() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1.5, 2.5, 3.5 });
        Assert.Equal(2.5, Expression.Eval<double>("arr[1]", context));
    }

    [Fact]
    public void ArrayIndex_IntArray() {
        var context = new ExpressionContext();
        context.Set("arr", new int[] { 10, 20, 30 });
        Assert.Equal(20L, Expression.Eval<long>("arr[1]", context));
    }

    [Fact]
    public void ArrayIndex_LongArray() {
        var context = new ExpressionContext();
        context.Set("arr", new long[] { 100, 200, 300 });
        Assert.Equal(200L, Expression.Eval<long>("arr[1]", context));
    }

    [Fact]
    public void ArrayIndex_ListDouble() {
        var context = new ExpressionContext();
        context.Set("arr", new List<double> { 1.1, 2.2, 3.3 });
        Assert.Equal(2.2, Expression.Eval<double>("arr[1]", context));
    }

    [Fact]
    public void ArrayIndex_ListLong() {
        var context = new ExpressionContext();
        context.Set("arr", new List<long> { 100, 200, 300 });
        Assert.Equal(200L, Expression.Eval<long>("arr[1]", context));
    }

    #endregion

    #region 异常场景

    [Fact]
    public void ArrayIndex_OutOfBounds_ThrowsException() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 2, 3 });
        Assert.Throws<EvaluateException>(() => Expression.Eval("arr[5]", context));
    }

    [Fact]
    public void ArrayIndex_NegativeIndex_ThrowsException() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 2, 3 });
        Assert.Throws<EvaluateException>(() => Expression.Eval("arr[-1]", context));
    }

    [Fact]
    public void ArrayIndex_ScalarWithIndex_ThrowsTypeMismatch() {
        var context = new ExpressionContext();
        context.Set("x", 42);
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("x[0]", context));
    }

    [Fact]
    public void ArrayIndex_UndefinedArray_ThrowsSymbolNotFound() {
        var context = new ExpressionContext();
        Assert.Throws<SymbolNotFoundException>(() => Expression.Eval("arr[0]", context));
    }

    [Fact]
    public void ArrayIndex_EmptyArray_ThrowsOutOfBounds() {
        var context = new ExpressionContext();
        context.Set("arr", new double[0]);
        Assert.Throws<EvaluateException>(() => Expression.Eval("arr[0]", context));
    }

    [Fact]
    public void ArrayIndex_BoundaryIndex_EqualsLength_ThrowsException() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 1, 2, 3 });
        Assert.Throws<EvaluateException>(() => Expression.Eval("arr[3]", context));
    }

    #endregion

    #region 边界与特殊场景

    [Fact]
    public void ArrayIndex_SingleElementArray() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 42 });
        Assert.Equal(42.0, Expression.Eval<double>("arr[0]", context));
    }

    [Fact]
    public void ArrayIndex_LargeArray() {
        var context = new ExpressionContext();
        var arr = Enumerable.Range(0, 1000).Select(i => (double)i).ToArray();
        context.Set("arr", arr);
        context.Set("i", 999);
        Assert.Equal(999.0, Expression.Eval<double>("arr[i]", context));
    }

    [Fact]
    public void ArrayIndex_WithSpaces() {
        var context = new ExpressionContext();
        context.Set("arr", new double[] { 10, 20, 30 });
        Assert.Equal(20.0, Expression.Eval<double>("arr [ 1 ]", context));
    }

    [Fact]
    public void ArrayIndex_BoolArray() {
        var context = new ExpressionContext();
        context.Set("flags", new bool[] { true, false, true });
        Assert.True(Expression.Eval<bool>("flags[0]", context));
        Assert.False(Expression.Eval<bool>("flags[1]", context));
    }

    [Fact]
    public void ArrayIndex_StringArray() {
        var context = new ExpressionContext();
        context.Set("names", new string[] { "hello", "world" });
        Assert.Equal("hello", Expression.Eval<string>("names[0]", context));
    }

    #endregion
}
