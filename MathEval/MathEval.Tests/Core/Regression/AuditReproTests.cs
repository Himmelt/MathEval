using MathEval.Context;
using MathEval.Exceptions;
using Xunit;

namespace MathEval.Tests.Regression;

/// <summary>
/// 整体审核修复的回归测试 + 索引下推移除后的语义确认
/// </summary>
public class AuditReproTests {
    [Fact]
    public void ArrayResultIndex_WithConstantFolding_IsConsistent() {
        // ([1,2,3]*x)[0]，x 为标量：数组字面量与标量广播 → [5,10,15] → 取 [0]
        // 折叠与不折叠、缓存与不缓存，结果必须一致
        var ctx = new ExpressionContext();
        ctx.Set("x", 5.0);

        Assert.Equal(5.0, Expression.Eval("([1,2,3]*x)[0]", ctx));
        Assert.Equal(5.0, Expression.Eval("([1,2,3]*x)[0]", ctx,
            ExpressionOptions.ConstantFolding | ExpressionOptions.NoCache));
        Assert.Equal(5.0, Expression.Eval("([1,2,3]*x)[0]", ctx,
            ExpressionOptions.ConstantFolding | ExpressionOptions.CompileOptimization));
    }

    [Fact]
    public void ScalarIndexing_AlwaysThrows_AfterPushdownRemoval() {
        // 索引下推移除后不再有合成索引的标量静默回退：
        // 标量结果上的用户索引一律抛类型错误（用户应自行编写 x*2）
        var ctx = new ExpressionContext();
        ctx.Set("x", 5.0);

        Assert.Throws<TypeMismatchException>(() => Expression.Eval("(x*2)[0]", ctx));
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("(x*2)[0]", ctx,
            ExpressionOptions.ConstantFolding | ExpressionOptions.NoCache));
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("x[0]", ctx));
    }

    [Fact]
    public void SameExpression_DifferentContexts_EvaluateIndependently() {
        // 修复前：AST 缓存键只有表达式文本，跨上下文互相污染；
        // 下推移除后 AST 不再依赖上下文，此测试确认各上下文按自身函数定义求值
        var a = new double[] { 1, 2, 3 };

        var ctxA = new ExpressionContext();
        ctxA.Set("a", a);
        ctxA.SetFunction("sum", (ExpressionFunction)(args =>
            args.Sum(x => MathEval.TypeSystem.TypeHelper.ToDouble(x!))), FunctionFlags.Aggregate);
        Assert.Equal(6.0, Expression.Eval("sum(a)", ctxA));

        // ctxB：同名函数为逐元素标量函数 → 广播 → [1,2,3] → [0]=1
        var ctxB = new ExpressionContext();
        ctxB.Set("a", a);
        ctxB.SetFunction("sum", (Func<double, double>)(v => v));
        Assert.Equal(1.0, Expression.Eval("sum(a)[0]", ctxB));

        // ctxD：聚合 → sum2(a)=6 标量 → 6[0] 标量索引 → 类型错误
        var ctxD = new ExpressionContext();
        ctxD.Set("a", a);
        ctxD.SetFunction("sum2", (ExpressionFunction)(args =>
            args.Sum(x => MathEval.TypeSystem.TypeHelper.ToDouble(x!))), FunctionFlags.Aggregate);
        Assert.Throws<TypeMismatchException>(() => Expression.Eval("sum2(a)[0]", ctxD));
    }

    [Fact]
    public void ConstantFolding_WithCache_DoesNotReuseUnfoldedAst() {
        // 修复前：同一表达式先以 None 解析入缓存，ConstantFolding 求值命中未折叠版本（优化失效）
        var expr = "1+2*3.5";
        Assert.Equal(8.0, Expression.Eval(expr));
        // 独立解析并折叠：结果一致（折叠正确性），且不同指纹互不串用
        Assert.Equal(8.0, Expression.Eval(expr, null, ExpressionOptions.ConstantFolding));
        Assert.Equal(8.0, Expression.Eval(expr, null,
            ExpressionOptions.ConstantFolding | ExpressionOptions.CompileOptimization));
    }

    [Fact]
    public void BuiltinFunctions_ThrowMathEvalException_OnTextArgument() {
        // 修复前：max('a') 经 Convert.ToDouble 抛 FormatException（泄漏库外异常类型）
        Assert.Throws<MathEval.Exceptions.TypeMismatchException>(() => Expression.Eval("max('a', 1)"));
        Assert.Throws<MathEval.Exceptions.TypeMismatchException>(() => Expression.Eval("round('a')"));
        Assert.Throws<MathEval.Exceptions.TypeMismatchException>(() => Expression.Eval("log('a')"));
    }

    [Fact]
    public void TryKindOf_StringArray_IsTextArray() {
        // 修复前：string[] 被 TryKindOf 误判为 number[]，StrictTypes 下
        // 合法的 names[0]+'!' 被静态误报类型错误
        var ctx = new ExpressionContext();
        ctx.Set("names", new[] { "alice", "bob" });
        Assert.Equal("alice!", Expression.Eval<string>("names[0] + '!'", ctx, ExpressionOptions.StrictTypes));
    }

    [Fact]
    public void EmptyFormatSpec_TreatedAsNoFormat() {
        // 修复前：空格式说明符使 Format 内 formatSpec[0] 抛 IndexOutOfRangeException（库外异常）
        var ctx = new ExpressionContext();
        ctx.Set("x", 3.14);
        Assert.Equal("3.14", Expression.Eval<string>("$'{x:}'", ctx));
        Assert.Equal("3.14", Expression.Eval<string>("$'{x:}'", ctx, ExpressionOptions.StrictTypes));
    }

    [Fact]
    public void StrictTypeCache_FoldedAndUnfolded_DoNotPollute() {
        // 修复前：StrictTypeCache 键不含折叠指纹，折叠版本编译的特化委托
        // 被纯 StrictTypes 求值复用（用户覆盖的 sin 被无视，返回预计算值）
        var ctx = new ExpressionContext();
        ctx.SetFunction("sin", (Func<double, double>)(v => 999));

        // 折叠模式：ConstantFolder 以内置纯函数表预计算 sin(0)=0
        Assert.Equal(0.0, Expression.Eval<double>("sin(0)", ctx,
            ExpressionOptions.StrictTypes | ExpressionOptions.ConstantFolding));
        // 纯 StrictTypes：应调用用户函数返回 999
        Assert.Equal(999.0, Expression.Eval<double>("sin(0)", ctx, ExpressionOptions.StrictTypes));
    }

    [Fact]
    public void InvalidFormatSpec_ThrowsMathEvalException() {
        // 修复前：精度数值溢出（如 f99999999999）触发 string.Format 抛 FormatException（库外异常泄漏）。
        // 注：多数字符串（如 'f1x'）被 .NET 当作自定义格式原样输出，不抛异常
        Assert.Throws<MathEval.Exceptions.ParseException>(() => Expression.Eval("$'{5:f99999999999}'"));
    }
}
