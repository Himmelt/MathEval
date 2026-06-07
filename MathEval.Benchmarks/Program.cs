using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using MathEval;
using MathEval.Context;
using MathEval.Fast;

BenchmarkRunner.Run<EvalBenchmarks>(args: args);

[MemoryDiagnoser]
[RankColumn]
public class EvalBenchmarks {
    private const string SimpleArithmetic = "1 + 2 * 3 - 4 / 2";
    private const string ComplexArithmetic = "(3.14 + 2.72) * (1.41 - 0.58) / (3.67 + 1.23) + 2.0 ^ 3.0";
    private const string FunctionCall = "sin(0.5) + cos(0.3) * sqrt(16) - abs(-5)";
    private const string NestedFunction = "sqrt(pow(sin(0.5), 2) + pow(cos(0.5), 2))";
    private const string Conditional = "1 > 0 ? 42 : -1";
    private const string Logical = "1 > 0 and 2 < 3 ? 100 : 0";
    private const string LogExpression = "log(100) + ln(2.718) + log(8, 2)";

    private ExpressionContext? _context;

    [GlobalSetup]
    public void Setup() {
        _context = new ExpressionContext();
    }

    // ===== 场景1: 重复求值同一表达式（MathEval有AST缓存） =====

    [Benchmark(Description = "Fast: 简单算术")]
    [BenchmarkCategory("Repeat")]
    public double Fast_SimpleArithmetic() => FastEval.EvalDouble(SimpleArithmetic);

    [Benchmark(Description = "MathEval: 简单算术")]
    [BenchmarkCategory("Repeat")]
    public object? MathEval_SimpleArithmetic() => Expression.Eval(SimpleArithmetic, _context);

    [Benchmark(Description = "Fast: 复杂算术")]
    [BenchmarkCategory("Repeat")]
    public double Fast_ComplexArithmetic() => FastEval.EvalDouble(ComplexArithmetic);

    [Benchmark(Description = "MathEval: 复杂算术")]
    [BenchmarkCategory("Repeat")]
    public object? MathEval_ComplexArithmetic() => Expression.Eval(ComplexArithmetic, _context);

    [Benchmark(Description = "Fast: 函数调用")]
    [BenchmarkCategory("Repeat")]
    public double Fast_FunctionCall() => FastEval.EvalDouble(FunctionCall);

    [Benchmark(Description = "MathEval: 函数调用")]
    [BenchmarkCategory("Repeat")]
    public object? MathEval_FunctionCall() => Expression.Eval(FunctionCall, _context);

    [Benchmark(Description = "Fast: 嵌套函数")]
    [BenchmarkCategory("Repeat")]
    public double Fast_NestedFunction() => FastEval.EvalDouble(NestedFunction);

    [Benchmark(Description = "MathEval: 嵌套函数")]
    [BenchmarkCategory("Repeat")]
    public object? MathEval_NestedFunction() => Expression.Eval(NestedFunction, _context);

    [Benchmark(Description = "Fast: 三元运算")]
    [BenchmarkCategory("Repeat")]
    public double Fast_Conditional() => FastEval.EvalDouble(Conditional);

    [Benchmark(Description = "MathEval: 三元运算")]
    [BenchmarkCategory("Repeat")]
    public object? MathEval_Conditional() => Expression.Eval(Conditional, _context);

    [Benchmark(Description = "Fast: 逻辑运算")]
    [BenchmarkCategory("Repeat")]
    public double Fast_Logical() => FastEval.EvalDouble(Logical);

    [Benchmark(Description = "MathEval: 逻辑运算")]
    [BenchmarkCategory("Repeat")]
    public object? MathEval_Logical() => Expression.Eval(Logical, _context);

    [Benchmark(Description = "Fast: 对数")]
    [BenchmarkCategory("Repeat")]
    public double Fast_Log() => FastEval.EvalDouble(LogExpression);

    [Benchmark(Description = "MathEval: 对数")]
    [BenchmarkCategory("Repeat")]
    public object? MathEval_Log() => Expression.Eval(LogExpression, _context);

    // ===== 场景2: 每次求值不同表达式（NoCache） =====

    private int _counter;

    [IterationSetup]
    public void IterationSetup() => _counter = 0;

    private string MakeUnique(string expr) {
        _counter++;
        return expr.Replace("0.5", (0.5 + _counter * 0.0001).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Benchmark(Description = "Fast(NoCache): 简单算术")]
    [BenchmarkCategory("NoCache")]
    public double Fast_NoCache_SimpleArithmetic() => FastEval.EvalDouble(MakeUnique(SimpleArithmetic));

    [Benchmark(Description = "MathEval(NoCache): 简单算术")]
    [BenchmarkCategory("NoCache")]
    public object? MathEval_NoCache_SimpleArithmetic() => Expression.Eval(MakeUnique(SimpleArithmetic), _context, ExpressionOptions.NoCache);

    [Benchmark(Description = "Fast(NoCache): 复杂算术")]
    [BenchmarkCategory("NoCache")]
    public double Fast_NoCache_ComplexArithmetic() => FastEval.EvalDouble(MakeUnique(ComplexArithmetic));

    [Benchmark(Description = "MathEval(NoCache): 复杂算术")]
    [BenchmarkCategory("NoCache")]
    public object? MathEval_NoCache_ComplexArithmetic() => Expression.Eval(MakeUnique(ComplexArithmetic), _context, ExpressionOptions.NoCache);

    [Benchmark(Description = "Fast(NoCache): 函数调用")]
    [BenchmarkCategory("NoCache")]
    public double Fast_NoCache_FunctionCall() => FastEval.EvalDouble(MakeUnique(FunctionCall));

    [Benchmark(Description = "MathEval(NoCache): 函数调用")]
    [BenchmarkCategory("NoCache")]
    public object? MathEval_NoCache_FunctionCall() => Expression.Eval(MakeUnique(FunctionCall), _context, ExpressionOptions.NoCache);

    [Benchmark(Description = "Fast(NoCache): 嵌套函数")]
    [BenchmarkCategory("NoCache")]
    public double Fast_NoCache_NestedFunction() => FastEval.EvalDouble(MakeUnique(NestedFunction));

    [Benchmark(Description = "MathEval(NoCache): 嵌套函数")]
    [BenchmarkCategory("NoCache")]
    public object? MathEval_NoCache_NestedFunction() => Expression.Eval(MakeUnique(NestedFunction), _context, ExpressionOptions.NoCache);

    [Benchmark(Description = "Fast(NoCache): 三元运算")]
    [BenchmarkCategory("NoCache")]
    public double Fast_NoCache_Conditional() => FastEval.EvalDouble(MakeUnique(Conditional));

    [Benchmark(Description = "MathEval(NoCache): 三元运算")]
    [BenchmarkCategory("NoCache")]
    public object? MathEval_NoCache_Conditional() => Expression.Eval(MakeUnique(Conditional), _context, ExpressionOptions.NoCache);

    [Benchmark(Description = "Fast(NoCache): 逻辑运算")]
    [BenchmarkCategory("NoCache")]
    public double Fast_NoCache_Logical() => FastEval.EvalDouble(MakeUnique(Logical));

    [Benchmark(Description = "MathEval(NoCache): 逻辑运算")]
    [BenchmarkCategory("NoCache")]
    public object? MathEval_NoCache_Logical() => Expression.Eval(MakeUnique(Logical), _context, ExpressionOptions.NoCache);

    [Benchmark(Description = "Fast(NoCache): 对数")]
    [BenchmarkCategory("NoCache")]
    public double Fast_NoCache_Log() => FastEval.EvalDouble(MakeUnique(LogExpression));

    [Benchmark(Description = "MathEval(NoCache): 对数")]
    [BenchmarkCategory("NoCache")]
    public object? MathEval_NoCache_Log() => Expression.Eval(MakeUnique(LogExpression), _context, ExpressionOptions.NoCache);
}
