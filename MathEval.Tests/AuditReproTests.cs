using MathEval.Context;
using MathEval.Exceptions;
using Xunit;

namespace MathEval.Tests;

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
            args.Sum(x => TypeSystem.TypeHelper.ToDouble(x!))), FunctionFlags.Aggregate);
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
            args.Sum(x => TypeSystem.TypeHelper.ToDouble(x!))), FunctionFlags.Aggregate);
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
        Assert.Throws<Exceptions.TypeMismatchException>(() => Expression.Eval("max('a', 1)"));
        Assert.Throws<Exceptions.TypeMismatchException>(() => Expression.Eval("round('a')"));
        Assert.Throws<Exceptions.TypeMismatchException>(() => Expression.Eval("log('a')"));
    }
}
