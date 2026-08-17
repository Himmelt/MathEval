using MathEval.Context;
using MathEval.Exceptions;
using Xunit;

namespace MathEval.Tests.TypeSystem;

/// <summary>
/// P2 文本数组：字面量 Kind 推断、TextArray 广播/聚合/索引
/// </summary>
public class TextArrayTests {
    #region 数组字面量 Kind 推断

    [Fact]
    public void TextArrayLiteral_ReturnsStringArray() {
        var result = Expression.Eval<string[]>("['a', 'b', 'c']");
        Assert.Equal(new[] { "a", "b", "c" }, result);
    }

    [Fact]
    public void TextArrayLiteral_AsList() {
        var result = Expression.Eval<List<string>>("['x', 'y']");
        Assert.Equal(new List<string> { "x", "y" }, result);
    }

    [Fact]
    public void NumberArrayLiteral_StillWorks() {
        var result = Expression.Eval<double[]>("[1, 2, 3]");
        Assert.Equal(new[] { 1.0, 2.0, 3.0 }, result);
    }

    [Fact]
    public void EmptyArrayLiteral_IsNumberArray() {
        var result = Expression.Eval<double[]>("[]");
        Assert.Empty(result);
    }

    [Fact]
    public void MixedArrayLiteral_Throws() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("['a', 1]"));
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("[1, 'a']"));
    }

    [Fact]
    public void NestedArrayLiteral_Throws() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("[[1], [2]]"));
    }

    #endregion

    #region 索引

    [Fact]
    public void TextArrayIndex_ReturnsText() {
        Assert.Equal("b", Expression.Eval<string>("['a', 'b', 'c'][1]"));
    }

    [Fact]
    public void TextArrayIndex_FromContextVariable() {
        var context = new ExpressionContext();
        context.Set("names", new[] { "Alice", "Bob" });
        Assert.Equal("Alice", Expression.Eval<string>("names[0]", context));
    }

    [Fact]
    public void TextArrayIndex_OutOfRange_Throws() {
        Assert.Throws<EvaluationException>(() => Expression.Eval("['a'][5]"));
    }

    [Fact]
    public void TextArrayIndex_ConcatAfterIndex() {
        Assert.Equal("b!", Expression.Eval<string>("['a', 'b'][1] + '!'"));
    }

    #endregion

    #region 广播：拼接与比较

    [Fact]
    public void TextArrayConcat_BothArrays() {
        var result = Expression.Eval<string[]>("['a', 'b'] + ['-', '+']");
        Assert.Equal(new[] { "a-", "b+" }, result);
    }

    [Fact]
    public void TextArrayConcat_ArrayAndScalar_Right() {
        var result = Expression.Eval<string[]>("['a', 'b'] + 'x'");
        Assert.Equal(new[] { "ax", "bx" }, result);
    }

    [Fact]
    public void TextArrayConcat_ScalarAndArray_Left() {
        var result = Expression.Eval<string[]>("'x' + ['a', 'b']");
        Assert.Equal(new[] { "xa", "xb" }, result);
    }

    [Fact]
    public void TextArrayConcat_LengthMismatch_Throws() {
        Assert.Throws<EvaluationException>(() => Expression.Eval("['a', 'b'] + ['c']"));
    }

    [Fact]
    public void TextArrayComparison_ElementWise() {
        Assert.Equal(new[] { 1.0, 0.0 }, Expression.Eval<double[]>("['a', 'b'] == ['a', 'c']"));
    }

    [Fact]
    public void TextArrayEqual_Broadcast() {
        Assert.Equal(new[] { 1.0, 0.0 }, Expression.Eval<double[]>("['a', 'b'] == 'a'"));
    }

    [Fact]
    public void TextArrayLessThan_Ordinal() {
        Assert.Equal(new[] { 1.0, 0.0 }, Expression.Eval<double[]>("['a', 'b'] < ['b', 'a']"));
    }

    [Fact]
    public void TextArrayArithmetic_Throws() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("['a'] * 2"));
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("['a'] - ['b']"));
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("-['a']"));
    }

    [Fact]
    public void NumberArrayPlusText_Throws() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("[1, 2] + 'a'"));
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("'a' + [1, 2]"));
    }

    #endregion

    #region 函数：广播与聚合

    [Fact]
    public void ElementWiseFunction_OnTextArray() {
        var context = new ExpressionContext();
        context.SetFunction("upper", (string s) => s.ToUpperInvariant());
        var result = Expression.Eval<string[]>("upper(['a', 'b'])", context);
        Assert.Equal(new[] { "A", "B" }, result);
    }

    [Fact]
    public void ElementWiseFunction_TextArrayAndScalarArgs() {
        var context = new ExpressionContext();
        context.SetFunction("repeat", (string s, double n) => string.Concat(System.Linq.Enumerable.Repeat(s, (int)n)));
        var result = Expression.Eval<string[]>("repeat(['a', 'b'], 2)", context);
        Assert.Equal(new[] { "aa", "bb" }, result);
    }

    [Fact]
    public void ElementWiseFunction_NumberResultOnTextArray() {
        var context = new ExpressionContext();
        context.SetFunction("len", (string s) => (double)s.Length);
        var result = Expression.Eval<double[]>("len(['a', 'bbb'])", context);
        Assert.Equal(new[] { 1.0, 3.0 }, result);
    }

    [Fact]
    public void AggregateFunction_ReceivesFlattenedTextArray() {
        var context = new ExpressionContext();
        context.SetFunction("join", args => string.Join("-", args.Select(a => a.ToString())),
            FunctionFlags.Aggregate);
        Assert.Equal("a-b-c", Expression.Eval<string>("join(['a', 'b', 'c'])", context));
    }

    [Fact]
    public void AggregateFunction_TextLengthSum() {
        var context = new ExpressionContext();
        context.SetFunction("totalLen", args => args.Length,
            FunctionFlags.Aggregate);
        Assert.Equal(3.0, Expression.Eval<double>("totalLen(['a', 'bb', 'ccc'])", context));
    }

    [Fact]
    public void TextArrayInLogicalContext_Throws() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("['a'] and [1]"));
    }

    #endregion

    #region 编译模式一致性

    [Fact]
    public void Compiled_TextArrayLiteralAndIndex() {
        Assert.Equal("b", Expression.OptimizedEval<string>("['a', 'b'][1]"));
    }

    [Fact]
    public void Compiled_TextArrayConcatBroadcast() {
        Assert.Equal(new[] { "ax", "bx" }, Expression.OptimizedEval<string[]>("['a', 'b'] + 'x'"));
        Assert.Equal(new[] { "xa", "xb" }, Expression.OptimizedEval<string[]>("'x' + ['a', 'b']"));
    }

    [Fact]
    public void Compiled_TextArrayComparison() {
        Assert.Equal(new[] { 1.0, 0.0 }, Expression.OptimizedEval<double[]>("[1, 0]"));
        Assert.Equal(new[] { 1.0, 0.0 }, Expression.OptimizedEval<double[]>("['a', 'b'] == 'a'"));
    }

    [Fact]
    public void Compiled_MixedArrayLiteral_Throws() {
        Assert.Throws<TypeMismatchException>(() => Expression.OptimizedEval("['a', 1]"));
    }

    [Fact]
    public void Compiled_ElementWiseFunction_OnTextArray() {
        var context = new ExpressionContext();
        context.SetFunction("upper", (string s) => s.ToUpperInvariant());
        Assert.Equal(new[] { "A", "B" }, Expression.OptimizedEval<string[]>("upper(['a', 'b'])", context));
    }

    [Fact]
    public void Compiled_AggregateFunction_TextArray() {
        var context = new ExpressionContext();
        context.SetFunction("join", args => string.Join("-", args.Select(a => a.ToString())),
            FunctionFlags.Aggregate);
        Assert.Equal("a-b", Expression.OptimizedEval<string>("join(['a', 'b'])", context));
    }

    #endregion
}
