using MathEval.Context;
using MathEval.Exceptions;
using Xunit;

namespace MathEval.Tests.Core;

/// <summary>
/// P4 StrictTypes：静态 Kind 推断（求值前类型检查、含死分支）、
/// SymbolVersion 缓存失效、纯 Number 特化的正确性
/// </summary>
public class StrictTypesTests {
    private static ExpressionOptions Strict => ExpressionOptions.StrictTypes;

    #region 求值前静态检查

    [Fact]
    public void NumberPlusText_ThrowsBeforeEvaluation() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("1 + 'a'", options: Strict));
    }

    [Fact]
    public void DeadBranch_IsAlsoChecked() {
        // 条件恒真，错误分支不会执行，但 StrictTypes 求值前检查整棵树
        var ctx = new ExpressionContext();
        ctx.Set("x", 10.0);
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("x > 0 ? 1 : (1 + 'a')", ctx, Strict));
    }

    [Fact]
    public void ShortCircuitRightOperand_IsAlsoChecked() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 10.0);
        // and 右侧短路后不会执行，但逻辑运算要求数值操作数
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("x > 0 and 'a'", ctx, Strict));
    }

    [Fact]
    public void BuiltinFunction_InvalidArgumentKind_Throws() {
        Assert.Throws<FunctionTypeMismatchException>(() => Expression.Eval("sin('abc')", options: Strict));
    }

    [Fact]
    public void MissingSymbol_ThrowsBeforeEvaluation() {
        Assert.Throws<SymbolNotFoundException>(() => Expression.Eval("a + 1", options: Strict));
    }

    [Fact]
    public void MissingFunction_ThrowsBeforeEvaluation() {
        Assert.Throws<FunctionNotFoundException>(() => Expression.Eval("nosuchfn(1)", options: Strict));
    }

    [Fact]
    public void TextMinusText_Throws() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("'a' - 'b'", options: Strict));
    }

    [Fact]
    public void UnaryNot_OnText_Throws() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("not 'a'", options: Strict));
    }

    [Fact]
    public void TextCondition_Throws() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("'a' ? 1 : 2", options: Strict));
    }

    [Fact]
    public void MixedArrayLiteral_Throws() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("[1, 'a']", options: Strict));
    }

    [Fact]
    public void IndexOnTextScalar_Throws() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("'abc'[0]", options: Strict));
    }

    [Fact]
    public void NumberArrayWithText_Throws() {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("[1,2] + 'a'", options: Strict));
    }

    [Fact]
    public void FormatSpecOnText_Throws() {
        Assert.Throws<EvaluateException>(() => Expression.Eval("$'{'a':f2}'", options: Strict));
    }

    [Fact]
    public void CustomFunction_InvalidArgumentKind_Throws() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("greet", (Func<string, string>)(s => s + "!"));
        Assert.Throws<FunctionTypeMismatchException>(() => Expression.Eval("greet(1)", ctx, Strict));
    }

    #endregion

    #region 合法表达式正常求值（含特化路径）

    [Fact]
    public void PureNumber_EvaluatesCorrectly() {
        Assert.Equal(3.0, Expression.Eval("1 + 2", options: Strict));
        Assert.Equal(11.0, Expression.Eval<double>("x * 2 + 1", NewCtx("x", 5.0), Strict));
    }

    [Fact]
    public void Specialized_ReuseAcrossValueUpdates() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 1.0);
        var calc = new Calculator("x * 2 + 1", ctx, Strict);
        Assert.Equal(3.0, calc.Eval<double>());

        // 值更新（Kind 不变）：版本失效重推断，特化委托复用，结果仍正确
        ctx.Set("x", 10.0);
        Assert.Equal(21.0, calc.Eval<double>());

        ctx.Set("x", -2.5);
        Assert.Equal(-4.0, calc.Eval<double>());
    }

    [Fact]
    public void SymbolKindChange_InvalidatesAndThrows() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 1.0);
        var calc = new Calculator("x * 2", ctx, Strict);
        Assert.Equal(2.0, calc.Eval<double>());

        // Kind 变化：重推断发现 x 为 text → 求值前抛出
        ctx.Set("x", "abc");
        Assert.Throws<TypeMismatchException>(() => calc.Eval());
    }

    [Fact]
    public void SymbolRemoved_Throws() {
        var ctx = new ExpressionContext();
        ctx.Set("x", 1.0);
        var calc = new Calculator("x + 1", ctx, Strict);
        Assert.Equal(2.0, calc.Eval<double>());

        ctx.RemoveSymbol("x");
        Assert.Throws<SymbolNotFoundException>(() => calc.Eval());
    }

    [Fact]
    public void Conditional_BothBranchesNumber_Specializes() {
        var ctx = NewCtx("x", 5.0);
        Assert.Equal(1.0, Expression.Eval("x > 0 ? 1 : 2", ctx, Strict));
        Assert.Equal(2.0, Expression.Eval("x > 0 ? 1 : 2", NewCtx("x", -5.0), Strict));
    }

    [Fact]
    public void Conditional_BranchesDifferentKind_IsLegal() {
        // 顶层返回各分支自身 Kind，静态不报错（外层运算时才可能受限）
        var ctx = NewCtx("x", 1.0);
        Assert.Equal("high", Expression.Eval("x > 0 ? 'high' : 'low'", ctx, Strict));
    }

    [Fact]
    public void LogicalAndOr_NumberOperands() {
        var ctx = NewCtx("x", 1.0);
        Assert.Equal(1.0, Expression.Eval("x > 0 and 2 > 1", ctx, Strict));
        Assert.Equal(0.0, Expression.Eval("x > 1 or 2 > 1 ? 0 : 1", ctx, Strict));
    }

    [Fact]
    public void BuiltinFunctions_NumberSignature() {
        var ctx = NewCtx("x", Math.PI / 2);
        Assert.Equal(1.0, Math.Round(Expression.Eval<double>("sin(x)", ctx, Strict), 10));
        Assert.Equal(3.0, Expression.Eval("max(1, 2, 3)", options: Strict));
        Assert.Equal(1.0, Expression.Eval("min(1, 2, 3)", options: Strict));
    }

    [Fact]
    public void TextOperations_StillWork() {
        Assert.Equal("ab", Expression.Eval("'a' + 'b'", options: Strict));
        Assert.Equal(1.0, Expression.Eval("'a' == 'a'", options: Strict));
        Assert.Equal(0.0, Expression.Eval("'a' < 'a'", options: Strict));
    }

    [Fact]
    public void ArrayOperations_StillWork() {
        var ctx = NewCtx("a", new double[] { 1, 2, 3 });
        Assert.Equal(new double[] { 2, 4, 6 }, Expression.Eval<double[]>("a * 2", ctx, Strict));
        Assert.Equal(2.0, Expression.Eval("a[1]", ctx, Strict));
        Assert.Equal(3.0, Expression.Eval("max(a)", ctx, Strict));
    }

    [Fact]
    public void Interpolation_StillWork() {
        var ctx = NewCtx("x", 1.5);
        Assert.Equal("x=1.5", Expression.Eval("$'x={x}'", ctx, Strict));
        Assert.Equal("x=1.50", Expression.Eval("$'x={x:f2}'", ctx, Strict));
    }

    [Fact]
    public void LazySymbol_UnknownKind_FallsBackAndEvaluates() {
        var ctx = new ExpressionContext();
        ctx.Set("r", (Func<object>)(() => 42.0));
        // 延迟符号无法静态确定 Kind → 不特化，但正常求值
        Assert.Equal(43.0, Expression.Eval("r + 1", ctx, Strict));
    }

    [Fact]
    public void CustomFunction_TextSignature_WorksWithStrictTypes() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("greet", (Func<string, string>)(s => "hi " + s));
        Assert.Equal("hi tom!", Expression.Eval("greet('tom') + '!'", ctx, Strict));
    }

    [Fact]
    public void CustomNumberFunction_Specializes() {
        var ctx = new ExpressionContext();
        ctx.SetFunction("twice", (Func<double, double>)(v => v * 2));
        Assert.Equal(10.0, Expression.Eval("twice(x) + 1", NewCtxOn(ctx, "x", 4.5), Strict));
    }

    #endregion

    #region 缓存与上下文隔离

    [Fact]
    public void SameExpression_DifferentContexts_Isolated() {
        var ctx1 = NewCtx("x", 1.0);
        var ctx2 = new ExpressionContext();

        Assert.Equal(2.0, Expression.Eval("x + 1", ctx1, Strict));

        // ctx2 无 x：同表达式在 ctx2 报符号缺失，且不影响 ctx1 缓存
        Assert.Throws<SymbolNotFoundException>(() => Expression.Eval("x + 1", ctx2, Strict));
        Assert.Equal(2.0, Expression.Eval("x + 1", ctx1, Strict));
    }

    [Fact]
    public void FunctionRegistered_AfterFirstFailure_Succeeds() {
        var ctx = new ExpressionContext();
        Assert.Throws<FunctionNotFoundException>(() => Expression.Eval("f(1) + 1", ctx, Strict));

        // 注册后版本递增 → 重推断通过
        ctx.SetFunction("f", (Func<double, double>)(v => v + 1));
        Assert.Equal(3.0, Expression.Eval("f(1) + 1", ctx, Strict));
    }

    [Fact]
    public void StrictTypes_CombinedWithCompileOptimization() {
        var opts = ExpressionOptions.StrictTypes | ExpressionOptions.CompileOptimization | ExpressionOptions.ConstantFolding;
        var ctx = NewCtx("x", 2.0);
        Assert.Equal(8.0, Expression.Eval("x ^ 3", ctx, opts));
        Assert.Equal("ab", Expression.Eval("'a' + 'b'", ctx, opts));
    }

    #endregion

    #region 非 Strict 模式行为一致（运行时兜底）

    [Theory]
    [InlineData("1 + 'a'")]
    [InlineData("'a' - 'b'")]
    public void NonStrict_RuntimeAlsoThrows(string expr) {
        Assert.Throws<TypeMismatchException>(() => Expression.Eval(expr));
    }

    [Fact]
    public void NonStrict_MissingSymbol_RuntimeThrows() {
        Assert.Throws<SymbolNotFoundException>(() => Expression.Eval("nosuchsym + 1"));
    }

    #endregion

    private static ExpressionContext NewCtx(string name, object value) {
        var ctx = new ExpressionContext();
        ctx.Set(name, value);
        return ctx;
    }

    private static ExpressionContext NewCtxOn(ExpressionContext ctx, string name, object value) {
        ctx.Set(name, value);
        return ctx;
    }
}
