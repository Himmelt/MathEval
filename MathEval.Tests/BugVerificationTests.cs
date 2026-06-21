using MathEval.AST;
using MathEval.Exceptions;
using MathEval.Fast;
using MathEval.Fast.Exceptions;
using MathEval.Fast.VM;
using MathEval.Optimization;
using System.Reflection;
using Xunit;
using LinqExprLexer = MathEval.Lexer.Lexer;
using LinqExprParser = MathEval.Parser.Parser;

namespace MathEval.Tests;

/// <summary>
/// BUG 验证测试：验证代码审查报告中识别的 14 个 BUG 的存在性。
/// 测试通过 = BUG 存在（验证 BUG 行为而非期望行为）。
/// 每个测试用注释说明正确行为 vs 实际 BUG 行为。
/// </summary>
public class BugVerificationTests {

    #region 严重 BUG（5 个）

    /// <summary>
    /// BUG-1：FastEvaluator 链式短路求值导致解析崩溃
    /// 短路时直接 return，未消费链中剩余操作符。
    /// 正确行为：1 || 2 || 3 → 短路返回 1.0
    /// BUG 行为：抛 FastEvalException（"|| 3" 未被消费）
    /// </summary>
    [Fact]
    public void Bug01_ChainedShortCircuitOrThrowsUnexpectedChar() {
        // 两个操作数不触发 BUG（正常工作）
        Assert.Equal(1.0, FastEval.EvalDouble("1 || 2"));

        // 三个操作数触发 BUG：短路后 "|| 3" 未被消费
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("1 || 2 || 3"));
    }

    /// <summary>
    /// BUG-1（补充）：链式 && 短路同样存在
    /// 正确行为：0 && 0 && 0 → 短路返回 0.0
    /// BUG 行为：抛 FastEvalException
    /// </summary>
    [Fact]
    public void Bug01_ChainedShortCircuitAndThrowsUnexpectedChar() {
        Assert.Equal(0.0, FastEval.EvalDouble("0 && 0"));

        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("0 && 0 && 0"));
    }

    /// <summary>
    /// BUG-2：FastEvaluator skipMode 下位运算/算术运算未跳过
    /// 三元 false 分支的位运算在 skipMode 下仍执行。
    /// 正确行为：1 ? 2 : 3.5 | 1 → 返回 2.0（false 分支被跳过）
    /// BUG 行为：抛 FastEvalException（3.5 非整数，位运算报错）
    /// </summary>
    [Fact]
    public void Bug02_SkipModeBitwiseNotSkipped() {
        // 正常场景：false 分支为合法整数位运算，不影响结果
        Assert.Equal(2.0, FastEval.EvalDouble("1 ? 2 : 3 | 1"));

        // BUG 场景：false 分支含非整数位运算，skipMode 未跳过
        Assert.Throws<FastEvalException>(() => FastEval.EvalDouble("1 ? 2 : 3.5 | 1"));
    }

    /// <summary>
    /// BUG-4：IndexPushdownOptimizer 无条件下推索引到函数参数（已修复）
    /// 修复后：聚合函数 max/min 跳过索引下推，保持原 AST 结构。
    /// max(a)[0] → ArrayIndexExpression(FunctionCall(max, a), 0)
    /// 而非被下推为 FunctionCall(max, ArrayIndexExpression(a, 0))
    /// </summary>
    [Fact]
    public void Bug04_IndexPushdownSkipsAggregateFunction() {
        // 构造 AST: max(a)[0]
        var original = new ArrayIndexExpression(
            new FunctionCall("max", [new Identifier("a")]),
            new ValueExpression(0.0));

        var optimized = IndexPushdownOptimizer.Optimize(original);

        // 修复后：聚合函数不被下推，根节点保持 ArrayIndexExpression
        Assert.IsType<ArrayIndexExpression>(optimized);
        var arrIdx = (ArrayIndexExpression)optimized;
        Assert.IsType<FunctionCall>(arrIdx.Array);
        var func = (FunctionCall)arrIdx.Array;
        Assert.Equal("max", func.Name);
        Assert.Single(func.Arguments);
        Assert.IsType<Identifier>(func.Arguments[0]); // a 未被索引化
    }

    /// <summary>
    /// BUG-5：CompiledExpression 编译模式缺少数组广播（已修复）
    /// 修复后：编译模式调用 CallFunctionWithBroadcast，行为匹配解释模式
    /// sin([1,2,3]) → 编译模式也做 element-wise 广播 → [sin(1), sin(2), sin(3)]
    /// max([1,2,3], 5) → 编译模式也展平 → max(1,2,3,5) = 5
    /// </summary>
    [Fact]
    public void Bug05_CompiledModeArrayBroadcast() {
        // 编译模式 - 广播 sin
        var compiledResult = Expression.OptimizedEval("sin([1, 2, 3])");
        Assert.IsType<double[]>(compiledResult);
        var compiledArr = (double[])compiledResult;
        Assert.Equal(3, compiledArr.Length);
        Assert.Equal(Math.Sin(1), compiledArr[0], 10);
        Assert.Equal(Math.Sin(2), compiledArr[1], 10);
        Assert.Equal(Math.Sin(3), compiledArr[2], 10);

        // 编译模式 - 聚合函数 max 展平数组
        var maxResult = Expression.OptimizedEval("max([1, 2, 3], 5)");
        Assert.Equal(5.0, maxResult);
    }

    #endregion

    #region 中等 BUG（4 个）

    /// <summary>
    /// BUG-6：主项目与 Fast 均遵循 IEEE 754 标准，除零返回 Infinity/NaN
    /// </summary>
    [Fact]
    public void Bug06_DivisionByZeroReturnsInfinity() {
        Assert.True(double.IsPositiveInfinity(FastEval.EvalDouble("1/0")));
        Assert.True(double.IsPositiveInfinity(Expression.Eval<double>("1/0", null, ExpressionOptions.NoCache)));
    }

    /// <summary>
    /// BUG-6（补充）：整除 // 同样返回 Infinity
    /// </summary>
    [Fact]
    public void Bug06_IntegerDivisionByZeroReturnsInfinity() {
        Assert.True(double.IsPositiveInfinity(FastEval.EvalDouble("1//0")));
        Assert.True(double.IsPositiveInfinity(Expression.Eval<double>("1//0", null, ExpressionOptions.NoCache)));
    }

    /// <summary>
    /// BUG-7：主项目与 Fast 均遵循 IEEE 754 标准，Pow 自然运算
    /// </summary>
    [Fact]
    public void Bug07_PowerNoValidation_NegativeBaseFractionalExp() {
        // 均返回 NaN（IEEE 754 标准），不抛异常
        Assert.True(double.IsNaN(FastEval.EvalDouble("(-2) ^ 0.5")));
        Assert.True(double.IsNaN(Expression.Eval<double>("(-2) ^ 0.5", null, ExpressionOptions.NoCache)));
    }

    /// <summary>
    /// BUG-7（补充）：零的负次幂返回 Infinity
    /// </summary>
    [Fact]
    public void Bug07_PowerNoValidation_ZeroNegativeExp() {
        Assert.True(double.IsPositiveInfinity(FastEval.EvalDouble("0 ^ -1")));
        Assert.True(double.IsPositiveInfinity(Expression.Eval<double>("0 ^ -1", null, ExpressionOptions.NoCache)));
    }

    /// <summary>
    /// BUG-8：CompiledExpression.CompileArrayIndex 未检查越界
    /// 编译模式使用原生数组访问，越界抛 IndexOutOfRangeException。
    /// 正确行为：[1,2,3][5] → EvaluateException（友好错误）
    /// BUG 行为：编译模式抛 IndexOutOfRangeException
    /// </summary>
    [Fact]
    public void Bug08_CompiledArrayIndexOutOfBoundsThrowsNativeException() {
        // 解释模式 - 正确抛 EvaluateException
        Assert.Throws<EvaluateException>(() =>
            Expression.Eval("[1, 2, 3][5]", null, ExpressionOptions.NoCache));

        // 编译模式 - 抛 IndexOutOfRangeException（BUG，应抛 EvaluateException）
        Assert.Throws<IndexOutOfRangeException>(() =>
            Expression.OptimizedEval("[1, 2, 3][5]"));
    }

    /// <summary>
    /// BUG-9：EvaluationVisitor 多数组广播未校验长度一致（已修复）
    /// 修复后：聚合函数不再进行 element-wise 广播，数组参数被展平后全局归约
    /// max([1,2,3], [1,2]) → flatten → max(1,2,3,1,2) → 3
    /// </summary>
    [Fact]
    public void Bug09_AggregateFunctionFlattensArrays() {
        Assert.Equal(3.0, Expression.Eval<double>("max([1, 2, 3], [1, 2])", null, ExpressionOptions.NoCache));
    }

    #endregion

    #region 轻微问题（5 个）

    /// <summary>
    /// BUG-10：BytecodeVM 返回值使用 stack[0] 而非 stack[sp-1]
    /// 正常编译 sp=1 时两者等价，但栈不平衡时返回错误值。
    /// 验证方式：直接构造 sp=3 的指令序列。
    /// 正确行为：返回 stack[sp-1] = stack[2] = 30
    /// BUG 行为：返回 stack[0] = 10
    /// </summary>
    [Fact]
    public void Bug10_BytecodeVMReturnsStack0InsteadOfStackSpMinus1() {
        // 构造指令序列：Push 10, Push 20, Push 30
        // 执行后 sp=3, stack=[10, 20, 30]
        var instructions = new Instruction[] {
            Instruction.Push(10.0),
            Instruction.Push(20.0),
            Instruction.Push(30.0),
        };

        var result = BytecodeVM.Execute(instructions, null);

        // BUG: 返回 stack[0]=10 而非 stack[sp-1]=30
        Assert.Equal(10.0, result);
    }

    /// <summary>
    /// BUG-11：FastScanner.ReadDecimal 未校验无效数字格式
    /// 1e、1e+、. 等无效格式被扫描器接受，double.Parse 抛 FormatException。
    /// 正确行为：抛 FastEvalException
    /// BUG 行为：抛 FormatException（非库统一异常）
    /// </summary>
    [Theory]
    [InlineData("1e")]
    [InlineData("1e+")]
    public void Bug11_FastScannerAcceptsInvalidNumberFormat(string expr) {
        // BUG: 抛 FormatException 而非 FastEvalException
        Assert.Throws<FormatException>(() => FastEval.EvalDouble(expr));
    }

    /// <summary>
    /// BUG-12：OptimizedExpressionCache.GetOrAddCompiled 存在竞态条件
    /// entry.Compiled == null 时多线程同时调用 compileFactory。
    /// 验证方式：先 Set AST（Compiled=null），再并发调用 GetOrAddCompiled。
    /// 正确行为：compileFactory 仅被调用一次
    /// BUG 行为：compileFactory 被多次调用
    /// </summary>
    [Fact]
    public void Bug12_CacheGetOrAddCompiledRaceCondition() {
        OptimizedExpressionCache.Clear();
        var expr = "1 + 2";

        // 先 Set AST（Compiled 为 null），模拟 Set 后的状态
        var lexer = new LinqExprLexer(expr);
        var parser = new LinqExprParser(lexer);
        var ast = IndexPushdownOptimizer.Optimize(parser.Parse());
        OptimizedExpressionCache.Set(expr, ast);

        int compileCount = 0;

        // 并发调用 GetOrAddCompiled
        Parallel.For(0, 100, _ => {
            OptimizedExpressionCache.GetOrAddCompiled(expr,
                e => {
                    var lx = new LinqExprLexer(e);
                    var pr = new LinqExprParser(lx);
                    return IndexPushdownOptimizer.Optimize(pr.Parse());
                },
                a => {
                    Interlocked.Increment(ref compileCount);
                    Thread.Sleep(50); // 增加竞态窗口
                    return new CompiledExpression(a);
                });
        });

        // BUG-12: 竞态导致 compileFactory 被多次调用
        Assert.True(compileCount > 1, $"竞态条件未触发: compileCount={compileCount}");
    }

    /// <summary>
    /// BUG-13：ExpressionCache 无容量限制，存在内存泄漏风险
    /// 静态 ConcurrentDictionary 无上限，缓存无限增长。
    /// 验证方式：求值多个不同表达式后通过反射检查缓存条目数。
    /// 正确行为：有 LRU 策略限制容量
    /// BUG 行为：所有条目都保留，无容量限制
    /// </summary>
    [Fact]
    public void Bug13_ExpressionCacheNoCapacityLimit() {
        var cacheType = Type.GetType("MathEval.Internal.ExpressionCache, MathEval");
        Assert.NotNull(cacheType);

        // 清空缓存
        var clearMethod = cacheType!.GetMethod("Clear", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        clearMethod!.Invoke(null, null);

        // 求值 200 个不同表达式
        for (int i = 0; i < 200; i++) {
            Expression.Eval<double>($"{i} * 2");
        }

        // 通过反射检查缓存条目数
        var cacheField = cacheType.GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(cacheField);
        var cache = cacheField!.GetValue(null);
        Assert.NotNull(cache);
        var countProperty = cache!.GetType().GetProperty("Count");
        var count = (int)countProperty!.GetValue(cache)!;

        // BUG-13: 缓存无容量限制，200 条全部保留
        Assert.True(count >= 200, $"缓存无容量限制，应有 >= 200 条，实际 {count}");
    }

    /// <summary>
    /// BUG-14：TypeMismatchException 继承层次可能引起混淆
    /// TypeMismatchException 直接继承 MathEvalException 而非 EvaluateException。
    /// 用户用 catch (EvaluateException) 捕获求值错误时会漏掉此异常。
    /// 注：ExceptionTests.cs 验证此为有意设计，此处仅验证继承关系事实。
    /// </summary>
    [Fact]
    public void Bug14_TypeMismatchExceptionDoesNotInheritEvaluateException() {
        // TypeMismatchException 在求值过程中抛出（如 ToInteger 失败）
        // 但不继承 EvaluateException
        Assert.False(typeof(EvaluateException).IsAssignableFrom(typeof(TypeMismatchException)),
            "TypeMismatchException 不继承 EvaluateException，catch(EvaluateException) 会漏掉此异常");

        // 验证：ToInteger 失败时抛出的 TypeMismatchException 无法被 catch(EvaluateException) 捕获
        try {
            Expression.Eval<double>("3.5 & 1", null, ExpressionOptions.NoCache);
            Assert.Fail("应抛出异常");
        } catch (EvaluateException) {
            Assert.Fail("TypeMismatchException 不应被 catch(EvaluateException) 捕获（BUG-14 验证）");
        } catch (TypeMismatchException) {
            // 预期：只能通过 catch(TypeMismatchException) 或 catch(MathEvalException) 捕获
        } catch (MathEvalException) {
            // 也可通过基类 MathEvalException 捕获
        }
    }

    #endregion
}