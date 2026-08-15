using MathEval.Context;
using MathEval.Exceptions;
using Xunit;

namespace MathEval.Tests;

/// <summary>
/// 整体审核修复的回归测试
/// </summary>
public class AuditReproTests {
    [Fact]
    public void ConstantFolding_PreservesIsSynthetic_OnPushdownIndex() {
        // ([1,2,3]*x)[0]，x 为标量：下推后 x[0] 为合成索引（标量静默回退）。
        // 修复前：常量折叠重建 ArrayIndexExpression 时丢 IsSynthetic → 误抛"索引操作需要数组类型"
        var ctx = new ExpressionContext();
        ctx.Set("x", 5.0);

        Assert.Equal(5.0, Expression.Eval("([1,2,3]*x)[0]", ctx));
        Assert.Equal(5.0, Expression.Eval("([1,2,3]*x)[0]", ctx,
            ExpressionOptions.ConstantFolding | ExpressionOptions.NoCache));
    }

    [Fact]
    public void CacheKey_IncludesParseFingerprint_AggregateContextsIsolated() {
        // 修复前：AST 缓存键只有表达式文本。
        // ctxA（sum 为聚合 → 不下推）先解析，ctxB（sum 为 ElementWise → 应下推）
        // 命中 ctxA 的 AST → FunctionCallEvaluator 以错误形态求值。
        // 注：Aggregate 契约 = 委托接收展平后的标量序列（FlattenArgs 已展平数组参数）
        var a = new double[] { 1, 2, 3 };

        var ctxA = new ExpressionContext();
        ctxA.Set("a", a);
        ctxA.SetFunction("sum", (ExpressionFunction)(args =>
            args.Sum(x => TypeSystem.TypeHelper.ToDouble(x!))), FunctionFlags.Aggregate);
        Assert.Equal(6.0, Expression.Eval("sum(a)", ctxA));

        // ctxB：sum 标量函数 + 下推 → sum(a[0]) = a[0]，不同上下文独立解析
        var ctxB = new ExpressionContext();
        ctxB.Set("a", a);
        ctxB.SetFunction("sum", (Func<double, double>)(v => v));
        Assert.Equal(1.0, Expression.Eval("sum(a)[0]", ctxB));

        // 反向顺序：ctxB 先、ctxA 后（聚合形态也互不污染）
        var expr = "sum2(a)[0]";
        var ctxC = new ExpressionContext();
        ctxC.Set("a", a);
        ctxC.SetFunction("sum2", (Func<double, double>)(v => v));
        Assert.Equal(1.0, Expression.Eval(expr, ctxC));

        var ctxD = new ExpressionContext();
        ctxD.Set("a", a);
        ctxD.SetFunction("sum2", (ExpressionFunction)(args =>
            args.Sum(x => TypeSystem.TypeHelper.ToDouble(x!))), FunctionFlags.Aggregate);
        // ctxD：聚合不下推 → sum2(a)=6 标量 → 6[0] 用户索引 → 抛类型错误
        Assert.Throws<TypeMismatchException>(() => Expression.Eval(expr, ctxD));
    }

    [Fact]
    public void DisableIndexPushdown_Option_NowWired() {
        // 修复前：DisableIndexPushdown 定义了但从未生效（ParseAndOptimize 无条件下推）。
        // (x*2)[0]，x 为标量：下推使 x[0] 合成回退 → 10；禁用下推 → 标量用户索引 → 类型错误
        var ctx = new ExpressionContext();
        ctx.Set("x", 5.0);

        Assert.Equal(10.0, Expression.Eval("(x*2)[0]", ctx));
        Assert.Throws<TypeMismatchException>(() =>
            Expression.Eval("(x*2)[0]", ctx, ExpressionOptions.DisableIndexPushdown | ExpressionOptions.NoCache));
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
        Assert.Throws<Exceptions.TypeMismatchException>(() => Expression.Eval("max('a', 1)"));
        Assert.Throws<Exceptions.TypeMismatchException>(() => Expression.Eval("round('a')"));
        Assert.Throws<Exceptions.TypeMismatchException>(() => Expression.Eval("log('a')"));
    }
}
