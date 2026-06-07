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
    private const string VariableExpr = "x * 2 + y - 3";

    private ExpressionContext? _context;
    private IReadOnlyDictionary<string, double>? _vars;
    private Func<IReadOnlyDictionary<string, double>?, double>? _compiledSimple;
    private Func<IReadOnlyDictionary<string, double>?, double>? _compiledComplex;
    private Func<IReadOnlyDictionary<string, double>?, double>? _compiledFunction;
    private Func<IReadOnlyDictionary<string, double>?, double>? _compiledNested;
    private Func<IReadOnlyDictionary<string, double>?, double>? _compiledVariable;

    [GlobalSetup]
    public void Setup() {
        _context = new ExpressionContext();
        _vars = new Dictionary<string, double> { ["x"] = 5.0, ["y"] = 3.0 };
        // 预编译委托，用于 JIT 执行测试
        _compiledSimple = FastEval.Compile(SimpleArithmetic);
        _compiledComplex = FastEval.Compile(ComplexArithmetic);
        _compiledFunction = FastEval.Compile(FunctionCall);
        _compiledNested = FastEval.Compile(NestedFunction);
        _compiledVariable = FastEval.Compile(VariableExpr);
    }

    // ============================================================
    //  场景1: 重复求值同一表达式（有缓存）
    //  对比 EvalDouble / EvalDoubleCached / Compile 执行 / MathEval
    // ============================================================

    [Benchmark(Description = "Fast.EvalDouble: 简单算术")]
    [BenchmarkCategory("Repeat")]
    public double Fast_SimpleArithmetic() => FastEval.EvalDouble(SimpleArithmetic);

    [Benchmark(Description = "Fast.Cached: 简单算术")]
    [BenchmarkCategory("Repeat")]
    public double Fast_Cached_SimpleArithmetic() => FastEval.EvalDoubleCached(SimpleArithmetic);

    [Benchmark(Description = "Fast.JIT: 简单算术")]
    [BenchmarkCategory("Repeat")]
    public double Fast_JIT_SimpleArithmetic() => _compiledSimple!(null);

    [Benchmark(Description = "MathEval: 简单算术")]
    [BenchmarkCategory("Repeat")]
    public object? MathEval_SimpleArithmetic() => Expression.Eval(SimpleArithmetic, _context);

    [Benchmark(Description = "Fast.EvalDouble: 复杂算术")]
    [BenchmarkCategory("Repeat")]
    public double Fast_ComplexArithmetic() => FastEval.EvalDouble(ComplexArithmetic);

    [Benchmark(Description = "Fast.Cached: 复杂算术")]
    [BenchmarkCategory("Repeat")]
    public double Fast_Cached_ComplexArithmetic() => FastEval.EvalDoubleCached(ComplexArithmetic);

    [Benchmark(Description = "Fast.JIT: 复杂算术")]
    [BenchmarkCategory("Repeat")]
    public double Fast_JIT_ComplexArithmetic() => _compiledComplex!(null);

    [Benchmark(Description = "MathEval: 复杂算术")]
    [BenchmarkCategory("Repeat")]
    public object? MathEval_ComplexArithmetic() => Expression.Eval(ComplexArithmetic, _context);

    [Benchmark(Description = "Fast.EvalDouble: 函数调用")]
    [BenchmarkCategory("Repeat")]
    public double Fast_FunctionCall() => FastEval.EvalDouble(FunctionCall);

    [Benchmark(Description = "Fast.Cached: 函数调用")]
    [BenchmarkCategory("Repeat")]
    public double Fast_Cached_FunctionCall() => FastEval.EvalDoubleCached(FunctionCall);

    [Benchmark(Description = "Fast.JIT: 函数调用")]
    [BenchmarkCategory("Repeat")]
    public double Fast_JIT_FunctionCall() => _compiledFunction!(null);

    [Benchmark(Description = "MathEval: 函数调用")]
    [BenchmarkCategory("Repeat")]
    public object? MathEval_FunctionCall() => Expression.Eval(FunctionCall, _context);

    [Benchmark(Description = "Fast.EvalDouble: 嵌套函数")]
    [BenchmarkCategory("Repeat")]
    public double Fast_NestedFunction() => FastEval.EvalDouble(NestedFunction);

    [Benchmark(Description = "Fast.Cached: 嵌套函数")]
    [BenchmarkCategory("Repeat")]
    public double Fast_Cached_NestedFunction() => FastEval.EvalDoubleCached(NestedFunction);

    [Benchmark(Description = "Fast.JIT: 嵌套函数")]
    [BenchmarkCategory("Repeat")]
    public double Fast_JIT_NestedFunction() => _compiledNested!(null);

    [Benchmark(Description = "MathEval: 嵌套函数")]
    [BenchmarkCategory("Repeat")]
    public object? MathEval_NestedFunction() => Expression.Eval(NestedFunction, _context);

    [Benchmark(Description = "Fast.EvalDouble: 三元运算")]
    [BenchmarkCategory("Repeat")]
    public double Fast_Conditional() => FastEval.EvalDouble(Conditional);

    [Benchmark(Description = "Fast.Cached: 三元运算")]
    [BenchmarkCategory("Repeat")]
    public double Fast_Cached_Conditional() => FastEval.EvalDoubleCached(Conditional);

    [Benchmark(Description = "MathEval: 三元运算")]
    [BenchmarkCategory("Repeat")]
    public object? MathEval_Conditional() => Expression.Eval(Conditional, _context);

    [Benchmark(Description = "Fast.EvalDouble: 逻辑运算")]
    [BenchmarkCategory("Repeat")]
    public double Fast_Logical() => FastEval.EvalDouble(Logical);

    [Benchmark(Description = "Fast.Cached: 逻辑运算")]
    [BenchmarkCategory("Repeat")]
    public double Fast_Cached_Logical() => FastEval.EvalDoubleCached(Logical);

    [Benchmark(Description = "MathEval: 逻辑运算")]
    [BenchmarkCategory("Repeat")]
    public object? MathEval_Logical() => Expression.Eval(Logical, _context);

    [Benchmark(Description = "Fast.EvalDouble: 对数")]
    [BenchmarkCategory("Repeat")]
    public double Fast_Log() => FastEval.EvalDouble(LogExpression);

    [Benchmark(Description = "Fast.Cached: 对数")]
    [BenchmarkCategory("Repeat")]
    public double Fast_Cached_Log() => FastEval.EvalDoubleCached(LogExpression);

    [Benchmark(Description = "MathEval: 对数")]
    [BenchmarkCategory("Repeat")]
    public object? MathEval_Log() => Expression.Eval(LogExpression, _context);

    // ============================================================
    //  场景2: 带变量的表达式（JIT 优势场景）
    // ============================================================

    [Benchmark(Description = "Fast.EvalDouble: 变量表达式")]
    [BenchmarkCategory("Variable")]
    public double Fast_Variable() => FastEval.EvalDouble(VariableExpr, _vars);

    [Benchmark(Description = "Fast.Cached: 变量表达式")]
    [BenchmarkCategory("Variable")]
    public double Fast_Cached_Variable() => FastEval.EvalDoubleCached(VariableExpr, _vars);

    [Benchmark(Description = "Fast.JIT: 变量表达式")]
    [BenchmarkCategory("Variable")]
    public double Fast_JIT_Variable() => _compiledVariable!(_vars);

    // ============================================================
    //  场景3: 编译开销（Compile vs CompileCached）
    // ============================================================

    [Benchmark(Description = "Fast.Compile: 编译开销")]
    [BenchmarkCategory("Compile")]
    public Func<IReadOnlyDictionary<string, double>?, double> Fast_Compile() => FastEval.Compile(FunctionCall);

    [Benchmark(Description = "Fast.CompileCached: 编译开销(缓存命中)")]
    [BenchmarkCategory("Compile")]
    public Func<IReadOnlyDictionary<string, double>?, double> Fast_CompileCached() => FastEval.CompileCached(FunctionCall);

    // ============================================================
    //  场景4: 每次求值不同表达式（无缓存）
    // ============================================================

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
}
