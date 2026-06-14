using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using MathEval.Fast;
using MathEvaluation.Context;
using MathEvaluation.Extensions;
using NCalc;
using NCalc.LambdaCompilation;
using NFun;
using System.Globalization;
using System.Text.RegularExpressions;

var config = DefaultConfig.Instance
    .AddExporter(JsonExporter.Full)
    .AddExporter(new HtmlReportExporter());
BenchmarkRunner.Run<CrossBenchmarks>(args: args, config: config);

/// <summary>
/// 三维交叉基准测试：[求值模式] × [表达式类型] × [库]
/// </summary>
[MemoryDiagnoser]
[RankColumn]
[CategoriesColumn]
public partial class CrossBenchmarks {
    // ============================================================
    // 表达式常量
    // ============================================================
    private const string SimpleArithmetic = "1 + 2 * 3 - 4 / 2";
    private const string ComplexArithmetic = "(3.14 + 2.72) * (1.41 - 0.58) / (3.67 + 1.23) + 2.0 ^ 3.0";
    private const string FunctionCall = "sin(0.5) + cos(0.3) * sqrt(16) - abs(-5)";
    private const string NestedFunction = "sqrt(pow(sin(0.5), 2) + pow(cos(0.5), 2))";
    private const string Conditional = "1 > 0 ? 42 : -1";
    private const string Logical = "1 > 0 and 2 < 3 ? 100 : 0";
    private const string LogExpression = "log(100) + ln(2.718) + log(8, 2)";

    // ============================================================
    // 上下文与状态
    // ============================================================

    // MathEval
    private MathEval.Context.ExpressionContext? _mathEvalCtx;

    // NCalc
    private readonly ExpressionContext _ncalcNoCacheCtx = new() { Options = ExpressionOptions.NoCache | ExpressionOptions.IgnoreCaseAtBuiltInFunctions };
    private readonly ExpressionContext _ncalcCachedCtx = new() { Options = ExpressionOptions.IgnoreCaseAtBuiltInFunctions };

    // NFun
    private IConstantCalculator<double>? _nfunConstantCalc;
    private ICalculator<object, double>? _nfunCalc;

    // MathEvaluator
    private readonly MathContext _mathEvalCtx2 = new ScientificMathContext();

    // 预编译委托 — MathEval.Fast
    private Func<IReadOnlyDictionary<string, double>?, double>? _fastCompiledSimple;
    private Func<IReadOnlyDictionary<string, double>?, double>? _fastCompiledComplex;
    private Func<IReadOnlyDictionary<string, double>?, double>? _fastCompiledFunction;
    private Func<IReadOnlyDictionary<string, double>?, double>? _fastCompiledNested;
    private Func<IReadOnlyDictionary<string, double>?, double>? _fastCompiledConditional;
    private Func<IReadOnlyDictionary<string, double>?, double>? _fastCompiledLogical;
    private Func<IReadOnlyDictionary<string, double>?, double>? _fastCompiledLog;

    // 预编译委托 — NCalc
    private Func<object?>? _ncalcCompiledSimple;
    private Func<object?>? _ncalcCompiledComplex;
    private Func<object?>? _ncalcCompiledFunction;
    private Func<object?>? _ncalcCompiledNested;
    private Func<object?>? _ncalcCompiledConditional;
    private Func<object?>? _ncalcCompiledLogical;
    private Func<object?>? _ncalcCompiledLog;

    // 预编译委托 — NFun
    private Func<object, double>? _nfunCompiledSimple;
    private Func<object, double>? _nfunCompiledComplex;
    private Func<object, double>? _nfunCompiledFunction;
    private Func<object, double>? _nfunCompiledNested;
    private Func<object, double>? _nfunCompiledConditional;
    private Func<object, double>? _nfunCompiledLogical;
    private Func<object, double>? _nfunCompiledLog;

    // 预编译委托 — MathEvaluator (Compile 返回 Func<anonymous, double>，用 Delegate 存储)
    private Delegate? _mathEvalCompiledSimple;
    private Delegate? _mathEvalCompiledComplex;
    private Delegate? _mathEvalCompiledFunction;
    private Delegate? _mathEvalCompiledNested;
    //private Delegate? _mathEvalCompiledConditional;
    //private Delegate? _mathEvalCompiledLogical;
    private Delegate? _mathEvalCompiledLog;

    // Interpret 模式用的计数器
    private int _counter;

    [GlobalSetup]
    public void Setup() {
        _mathEvalCtx = new MathEval.Context.ExpressionContext();

        // NFun
        _nfunConstantCalc = Funny.BuildForCalcConstant<double>();
        _nfunCalc = Funny.BuildForCalc<object, double>();

        // MathEval.Fast 预编译
        _fastCompiledSimple = FastEval.CompileCached(SimpleArithmetic);
        _fastCompiledComplex = FastEval.CompileCached(ComplexArithmetic);
        _fastCompiledFunction = FastEval.CompileCached(FunctionCall);
        _fastCompiledNested = FastEval.CompileCached(NestedFunction);
        _fastCompiledConditional = FastEval.CompileCached(Conditional);
        _fastCompiledLogical = FastEval.CompileCached(Logical);
        _fastCompiledLog = FastEval.CompileCached(LogExpression);

        // NCalc 预编译（LambdaCompilation 中 ^ 被当作 XOR，需用 ** 替代）
        _ncalcCompiledSimple = new Expression(NCalcCompileTransform(SimpleArithmetic), _ncalcCachedCtx).ToLambda<object>();
        _ncalcCompiledComplex = new Expression(NCalcCompileTransform(ComplexArithmetic), _ncalcCachedCtx).ToLambda<object>();
        _ncalcCompiledFunction = new Expression(NCalcCompileTransform(FunctionCall), _ncalcCachedCtx).ToLambda<object>();
        _ncalcCompiledNested = new Expression(NCalcCompileTransform(NestedFunction), _ncalcCachedCtx).ToLambda<object>();
        _ncalcCompiledConditional = new Expression(NCalcCompileTransform(Conditional), _ncalcCachedCtx).ToLambda<object>();
        _ncalcCompiledLogical = new Expression(NCalcCompileTransform(Logical), _ncalcCachedCtx).ToLambda<object>();
        _ncalcCompiledLog = new Expression(NCalcCompileTransform(LogExpression), _ncalcCachedCtx).ToLambda<object>();

        // NFun 预编译
        _nfunCompiledSimple = _nfunCalc.ToLambda(NFunTransform(SimpleArithmetic));
        _nfunCompiledComplex = _nfunCalc.ToLambda(NFunTransform(ComplexArithmetic));
        _nfunCompiledFunction = _nfunCalc.ToLambda(NFunTransform(FunctionCall));
        _nfunCompiledNested = _nfunCalc.ToLambda(NFunTransform(NestedFunction));
        _nfunCompiledConditional = _nfunCalc.ToLambda(NFunTransform(Conditional));
        _nfunCompiledLogical = _nfunCalc.ToLambda(NFunTransform(Logical));
        _nfunCompiledLog = _nfunCalc.ToLambda(NFunTransform(LogExpression));

        // MathEvaluator 预编译（不支持 Conditional/Logical 的 ? : 和 and 语法）
        _mathEvalCompiledSimple = SimpleArithmetic.Compile(new { }, _mathEvalCtx2);
        _mathEvalCompiledComplex = ComplexArithmetic.Compile(new { }, _mathEvalCtx2);
        _mathEvalCompiledFunction = FunctionCall.Compile(new { }, _mathEvalCtx2);
        _mathEvalCompiledNested = NestedFunction.Compile(new { }, _mathEvalCtx2);
        //_mathEvalCompiledConditional = null; // MathEvaluator 不支持 ? : 三元运算
        //_mathEvalCompiledLogical = null;     // MathEvaluator 不支持 and 逻辑运算
        _mathEvalCompiledLog = MathEvaluatorTransform(LogExpression).Compile(new { }, _mathEvalCtx2);
    }

    [IterationSetup]
    public void IterationSetup() => _counter = 0;

    private string MakeUnique(string expr) {
        _counter++;
        return expr.Replace("0.5", (0.5 + _counter * 0.0001).ToString(CultureInfo.InvariantCulture));
    }

    // ============================================================
    // NFun 语法转换
    // ============================================================
    private static string NFunTransform(string expr) {
        var result = expr.Replace("^", "**");
        // pow(sin(x), 2) -> (sin(x)**2), pow(cos(x), 2) -> (cos(x)**2)
        result = PowRegex().Replace(result, m => {
            var inner = m.Value.Replace("pow(", "").Replace(", 2)", "");
            return $"({inner}**2)";
        });
        // 三元运算 a ? b : c -> if (a) b else c
        if (result.Contains('?')) {
            var parts = result.Split('?');
            var condition = parts[0].Trim();
            var rest = parts[1].Split(':');
            var trueVal = rest[0].Trim();
            var falseVal = rest[1].Trim();
            result = $"if ({condition}) {trueVal} else {falseVal}";
        }
        // log(100) -> log10(100), ln(x) -> log(x), log(8, 2) -> (log(8)/log(2))
        result = result.Replace("log(100)", "log10(100)");
        result = result.Replace("ln(", "log(");
        result = result.Replace("log(8, 2)", "(log(8)/log(2))");
        return result;
    }

    // ============================================================
    // NCalc 语法转换（LambdaCompilation 中 ^ 被当作 XOR，需替换为 **；
    // log(x) 不支持需转为 Log10(x)；ln(x) 需转为 Log(x, E)）
    // ============================================================
    private static string NCalcCompileTransform(string expr) {
        var result = expr.Replace("^", "**");
        result = result.Replace("log(100)", "Log10(100)");
        result = result.Replace("ln(2.718)", "Log(2.718, 2.718281828459045)");
        return result;
    }

    // NCalc Interpret/Cached 模式：^ 是幂运算不需要转换，但 log/ln 需要转换
    private static string NCalcInterpretTransform(string expr) {
        var result = expr.Replace("log(100)", "Log10(100)");
        result = result.Replace("ln(2.718)", "Log(2.718, 2.718281828459045)");
        return result;
    }

    // ============================================================
    // MathEvaluator 语法转换（不支持 log(x, base) 双参数、ln；
    // log(x) = 自然对数，log10(x) = 常用对数）
    // ============================================================
    private static string MathEvaluatorTransform(string expr) {
        var result = expr.Replace("log(8, 2)", "(log(8)/log(2))");
        result = result.Replace("log(100)", "log10(100)");
        result = result.Replace("ln(", "log(");
        return result;
    }

    [GeneratedRegex(@"pow\((?:sin|cos)\([^)]+\),\s*2\)")]
    private static partial Regex PowRegex();

    // ============================================================
    // MathEval — Interpret (NoCache)
    // ============================================================

    [Benchmark(Description = "MathEval: Interpret/简单算术")]
    [BenchmarkCategory("MathEval", "Interpret", "SimpleArithmetic")]
    public object? MathEval_Interpret_SimpleArithmetic()
        => MathEval.Expression.Eval(MakeUnique(SimpleArithmetic), _mathEvalCtx, MathEval.ExpressionOptions.NoCache);

    [Benchmark(Description = "MathEval: Interpret/复杂算术")]
    [BenchmarkCategory("MathEval", "Interpret", "ComplexArithmetic")]
    public object? MathEval_Interpret_ComplexArithmetic()
        => MathEval.Expression.Eval(MakeUnique(ComplexArithmetic), _mathEvalCtx, MathEval.ExpressionOptions.NoCache);

    [Benchmark(Description = "MathEval: Interpret/函数调用")]
    [BenchmarkCategory("MathEval", "Interpret", "FunctionCall")]
    public object? MathEval_Interpret_FunctionCall()
        => MathEval.Expression.Eval(MakeUnique(FunctionCall), _mathEvalCtx, MathEval.ExpressionOptions.NoCache);

    [Benchmark(Description = "MathEval: Interpret/嵌套函数")]
    [BenchmarkCategory("MathEval", "Interpret", "NestedFunction")]
    public object? MathEval_Interpret_NestedFunction()
        => MathEval.Expression.Eval(MakeUnique(NestedFunction), _mathEvalCtx, MathEval.ExpressionOptions.NoCache);

    [Benchmark(Description = "MathEval: Interpret/三元运算")]
    [BenchmarkCategory("MathEval", "Interpret", "Conditional")]
    public object? MathEval_Interpret_Conditional()
        => MathEval.Expression.Eval(MakeUnique(Conditional), _mathEvalCtx, MathEval.ExpressionOptions.NoCache);

    [Benchmark(Description = "MathEval: Interpret/逻辑运算")]
    [BenchmarkCategory("MathEval", "Interpret", "Logical")]
    public object? MathEval_Interpret_Logical()
        => MathEval.Expression.Eval(MakeUnique(Logical), _mathEvalCtx, MathEval.ExpressionOptions.NoCache);

    [Benchmark(Description = "MathEval: Interpret/对数")]
    [BenchmarkCategory("MathEval", "Interpret", "Log")]
    public object? MathEval_Interpret_Log()
        => MathEval.Expression.Eval(MakeUnique(LogExpression), _mathEvalCtx, MathEval.ExpressionOptions.NoCache);

    // ============================================================
    // MathEval — Cached
    // ============================================================

    [Benchmark(Description = "MathEval: Cached/简单算术")]
    [BenchmarkCategory("MathEval", "Cached", "SimpleArithmetic")]
    public object? MathEval_Cached_SimpleArithmetic()
        => MathEval.Expression.Eval(SimpleArithmetic, _mathEvalCtx);

    [Benchmark(Description = "MathEval: Cached/复杂算术")]
    [BenchmarkCategory("MathEval", "Cached", "ComplexArithmetic")]
    public object? MathEval_Cached_ComplexArithmetic()
        => MathEval.Expression.Eval(ComplexArithmetic, _mathEvalCtx);

    [Benchmark(Description = "MathEval: Cached/函数调用")]
    [BenchmarkCategory("MathEval", "Cached", "FunctionCall")]
    public object? MathEval_Cached_FunctionCall()
        => MathEval.Expression.Eval(FunctionCall, _mathEvalCtx);

    [Benchmark(Description = "MathEval: Cached/嵌套函数")]
    [BenchmarkCategory("MathEval", "Cached", "NestedFunction")]
    public object? MathEval_Cached_NestedFunction()
        => MathEval.Expression.Eval(NestedFunction, _mathEvalCtx);

    [Benchmark(Description = "MathEval: Cached/三元运算")]
    [BenchmarkCategory("MathEval", "Cached", "Conditional")]
    public object? MathEval_Cached_Conditional()
        => MathEval.Expression.Eval(Conditional, _mathEvalCtx);

    [Benchmark(Description = "MathEval: Cached/逻辑运算")]
    [BenchmarkCategory("MathEval", "Cached", "Logical")]
    public object? MathEval_Cached_Logical()
        => MathEval.Expression.Eval(Logical, _mathEvalCtx);

    [Benchmark(Description = "MathEval: Cached/对数")]
    [BenchmarkCategory("MathEval", "Cached", "Log")]
    public object? MathEval_Cached_Log()
        => MathEval.Expression.Eval(LogExpression, _mathEvalCtx);

    // ============================================================
    // MathEval — Compile (OptimizedEval)
    // ============================================================

    [Benchmark(Description = "MathEval: Compile/简单算术")]
    [BenchmarkCategory("MathEval", "Compile", "SimpleArithmetic")]
    public object? MathEval_Compile_SimpleArithmetic()
        => MathEval.Expression.OptimizedEval(SimpleArithmetic, _mathEvalCtx);

    [Benchmark(Description = "MathEval: Compile/复杂算术")]
    [BenchmarkCategory("MathEval", "Compile", "ComplexArithmetic")]
    public object? MathEval_Compile_ComplexArithmetic()
        => MathEval.Expression.OptimizedEval(ComplexArithmetic, _mathEvalCtx);

    [Benchmark(Description = "MathEval: Compile/函数调用")]
    [BenchmarkCategory("MathEval", "Compile", "FunctionCall")]
    public object? MathEval_Compile_FunctionCall()
        => MathEval.Expression.OptimizedEval(FunctionCall, _mathEvalCtx);

    [Benchmark(Description = "MathEval: Compile/嵌套函数")]
    [BenchmarkCategory("MathEval", "Compile", "NestedFunction")]
    public object? MathEval_Compile_NestedFunction()
        => MathEval.Expression.OptimizedEval(NestedFunction, _mathEvalCtx);

    [Benchmark(Description = "MathEval: Compile/三元运算")]
    [BenchmarkCategory("MathEval", "Compile", "Conditional")]
    public object? MathEval_Compile_Conditional()
        => MathEval.Expression.OptimizedEval(Conditional, _mathEvalCtx);

    [Benchmark(Description = "MathEval: Compile/逻辑运算")]
    [BenchmarkCategory("MathEval", "Compile", "Logical")]
    public object? MathEval_Compile_Logical()
        => MathEval.Expression.OptimizedEval(Logical, _mathEvalCtx);

    [Benchmark(Description = "MathEval: Compile/对数")]
    [BenchmarkCategory("MathEval", "Compile", "Log")]
    public object? MathEval_Compile_Log()
        => MathEval.Expression.OptimizedEval(LogExpression, _mathEvalCtx);

    // ============================================================
    // MathEval.Fast — Interpret
    // ============================================================

    [Benchmark(Description = "Fast: Interpret/简单算术")]
    [BenchmarkCategory("MathEval.Fast", "Interpret", "SimpleArithmetic")]
    public double Fast_Interpret_SimpleArithmetic() => FastEval.EvalDouble(MakeUnique(SimpleArithmetic));

    [Benchmark(Description = "Fast: Interpret/复杂算术")]
    [BenchmarkCategory("MathEval.Fast", "Interpret", "ComplexArithmetic")]
    public double Fast_Interpret_ComplexArithmetic() => FastEval.EvalDouble(MakeUnique(ComplexArithmetic));

    [Benchmark(Description = "Fast: Interpret/函数调用")]
    [BenchmarkCategory("MathEval.Fast", "Interpret", "FunctionCall")]
    public double Fast_Interpret_FunctionCall() => FastEval.EvalDouble(MakeUnique(FunctionCall));

    [Benchmark(Description = "Fast: Interpret/嵌套函数")]
    [BenchmarkCategory("MathEval.Fast", "Interpret", "NestedFunction")]
    public double Fast_Interpret_NestedFunction() => FastEval.EvalDouble(MakeUnique(NestedFunction));

    [Benchmark(Description = "Fast: Interpret/三元运算")]
    [BenchmarkCategory("MathEval.Fast", "Interpret", "Conditional")]
    public double Fast_Interpret_Conditional() => FastEval.EvalDouble(MakeUnique(Conditional));

    [Benchmark(Description = "Fast: Interpret/逻辑运算")]
    [BenchmarkCategory("MathEval.Fast", "Interpret", "Logical")]
    public double Fast_Interpret_Logical() => FastEval.EvalDouble(MakeUnique(Logical));

    [Benchmark(Description = "Fast: Interpret/对数")]
    [BenchmarkCategory("MathEval.Fast", "Interpret", "Log")]
    public double Fast_Interpret_Log() => FastEval.EvalDouble(MakeUnique(LogExpression));

    // ============================================================
    // MathEval.Fast — Cached
    // ============================================================

    [Benchmark(Description = "Fast: Cached/简单算术")]
    [BenchmarkCategory("MathEval.Fast", "Cached", "SimpleArithmetic")]
    public double Fast_Cached_SimpleArithmetic() => FastEval.EvalDoubleCached(SimpleArithmetic);

    [Benchmark(Description = "Fast: Cached/复杂算术")]
    [BenchmarkCategory("MathEval.Fast", "Cached", "ComplexArithmetic")]
    public double Fast_Cached_ComplexArithmetic() => FastEval.EvalDoubleCached(ComplexArithmetic);

    [Benchmark(Description = "Fast: Cached/函数调用")]
    [BenchmarkCategory("MathEval.Fast", "Cached", "FunctionCall")]
    public double Fast_Cached_FunctionCall() => FastEval.EvalDoubleCached(FunctionCall);

    [Benchmark(Description = "Fast: Cached/嵌套函数")]
    [BenchmarkCategory("MathEval.Fast", "Cached", "NestedFunction")]
    public double Fast_Cached_NestedFunction() => FastEval.EvalDoubleCached(NestedFunction);

    [Benchmark(Description = "Fast: Cached/三元运算")]
    [BenchmarkCategory("MathEval.Fast", "Cached", "Conditional")]
    public double Fast_Cached_Conditional() => FastEval.EvalDoubleCached(Conditional);

    [Benchmark(Description = "Fast: Cached/逻辑运算")]
    [BenchmarkCategory("MathEval.Fast", "Cached", "Logical")]
    public double Fast_Cached_Logical() => FastEval.EvalDoubleCached(Logical);

    [Benchmark(Description = "Fast: Cached/对数")]
    [BenchmarkCategory("MathEval.Fast", "Cached", "Log")]
    public double Fast_Cached_Log() => FastEval.EvalDoubleCached(LogExpression);

    // ============================================================
    // MathEval.Fast — Compile
    // ============================================================

    [Benchmark(Description = "Fast: Compile/简单算术")]
    [BenchmarkCategory("MathEval.Fast", "Compile", "SimpleArithmetic")]
    public double Fast_Compile_SimpleArithmetic() => _fastCompiledSimple!(null);

    [Benchmark(Description = "Fast: Compile/复杂算术")]
    [BenchmarkCategory("MathEval.Fast", "Compile", "ComplexArithmetic")]
    public double Fast_Compile_ComplexArithmetic() => _fastCompiledComplex!(null);

    [Benchmark(Description = "Fast: Compile/函数调用")]
    [BenchmarkCategory("MathEval.Fast", "Compile", "FunctionCall")]
    public double Fast_Compile_FunctionCall() => _fastCompiledFunction!(null);

    [Benchmark(Description = "Fast: Compile/嵌套函数")]
    [BenchmarkCategory("MathEval.Fast", "Compile", "NestedFunction")]
    public double Fast_Compile_NestedFunction() => _fastCompiledNested!(null);

    [Benchmark(Description = "Fast: Compile/三元运算")]
    [BenchmarkCategory("MathEval.Fast", "Compile", "Conditional")]
    public double Fast_Compile_Conditional() => _fastCompiledConditional!(null);

    [Benchmark(Description = "Fast: Compile/逻辑运算")]
    [BenchmarkCategory("MathEval.Fast", "Compile", "Logical")]
    public double Fast_Compile_Logical() => _fastCompiledLogical!(null);

    [Benchmark(Description = "Fast: Compile/对数")]
    [BenchmarkCategory("MathEval.Fast", "Compile", "Log")]
    public double Fast_Compile_Log() => _fastCompiledLog!(null);

    // ============================================================
    // NCalc — Interpret (NoCache)
    // ============================================================

    [Benchmark(Description = "NCalc: Interpret/简单算术")]
    [BenchmarkCategory("NCalc", "Interpret", "SimpleArithmetic")]
    public object? NCalc_Interpret_SimpleArithmetic()
        => new Expression(MakeUnique(SimpleArithmetic), _ncalcNoCacheCtx).Evaluate();

    [Benchmark(Description = "NCalc: Interpret/复杂算术")]
    [BenchmarkCategory("NCalc", "Interpret", "ComplexArithmetic")]
    public object? NCalc_Interpret_ComplexArithmetic()
        => new Expression(MakeUnique(ComplexArithmetic), _ncalcNoCacheCtx).Evaluate();

    [Benchmark(Description = "NCalc: Interpret/函数调用")]
    [BenchmarkCategory("NCalc", "Interpret", "FunctionCall")]
    public object? NCalc_Interpret_FunctionCall()
        => new Expression(MakeUnique(FunctionCall), _ncalcNoCacheCtx).Evaluate();

    [Benchmark(Description = "NCalc: Interpret/嵌套函数")]
    [BenchmarkCategory("NCalc", "Interpret", "NestedFunction")]
    public object? NCalc_Interpret_NestedFunction()
        => new Expression(MakeUnique(NestedFunction), _ncalcNoCacheCtx).Evaluate();

    [Benchmark(Description = "NCalc: Interpret/三元运算")]
    [BenchmarkCategory("NCalc", "Interpret", "Conditional")]
    public object? NCalc_Interpret_Conditional()
        => new Expression(MakeUnique(Conditional), _ncalcNoCacheCtx).Evaluate();

    [Benchmark(Description = "NCalc: Interpret/逻辑运算")]
    [BenchmarkCategory("NCalc", "Interpret", "Logical")]
    public object? NCalc_Interpret_Logical()
        => new Expression(MakeUnique(Logical), _ncalcNoCacheCtx).Evaluate();

    [Benchmark(Description = "NCalc: Interpret/对数")]
    [BenchmarkCategory("NCalc", "Interpret", "Log")]
    public object? NCalc_Interpret_Log()
        => new Expression(NCalcInterpretTransform(MakeUnique(LogExpression)), _ncalcNoCacheCtx).Evaluate();

    // ============================================================
    // NCalc — Cached
    // ============================================================

    [Benchmark(Description = "NCalc: Cached/简单算术")]
    [BenchmarkCategory("NCalc", "Cached", "SimpleArithmetic")]
    public object? NCalc_Cached_SimpleArithmetic()
        => new Expression(SimpleArithmetic, _ncalcCachedCtx).Evaluate();

    [Benchmark(Description = "NCalc: Cached/复杂算术")]
    [BenchmarkCategory("NCalc", "Cached", "ComplexArithmetic")]
    public object? NCalc_Cached_ComplexArithmetic()
        => new Expression(ComplexArithmetic, _ncalcCachedCtx).Evaluate();

    [Benchmark(Description = "NCalc: Cached/函数调用")]
    [BenchmarkCategory("NCalc", "Cached", "FunctionCall")]
    public object? NCalc_Cached_FunctionCall()
        => new Expression(FunctionCall, _ncalcCachedCtx).Evaluate();

    [Benchmark(Description = "NCalc: Cached/嵌套函数")]
    [BenchmarkCategory("NCalc", "Cached", "NestedFunction")]
    public object? NCalc_Cached_NestedFunction()
        => new Expression(NestedFunction, _ncalcCachedCtx).Evaluate();

    [Benchmark(Description = "NCalc: Cached/三元运算")]
    [BenchmarkCategory("NCalc", "Cached", "Conditional")]
    public object? NCalc_Cached_Conditional()
        => new Expression(Conditional, _ncalcCachedCtx).Evaluate();

    [Benchmark(Description = "NCalc: Cached/逻辑运算")]
    [BenchmarkCategory("NCalc", "Cached", "Logical")]
    public object? NCalc_Cached_Logical()
        => new Expression(Logical, _ncalcCachedCtx).Evaluate();

    [Benchmark(Description = "NCalc: Cached/对数")]
    [BenchmarkCategory("NCalc", "Cached", "Log")]
    public object? NCalc_Cached_Log()
        => new Expression(NCalcInterpretTransform(LogExpression), _ncalcCachedCtx).Evaluate();

    // ============================================================
    // NCalc — Compile (ToLambda)
    // ============================================================

    [Benchmark(Description = "NCalc: Compile/简单算术")]
    [BenchmarkCategory("NCalc", "Compile", "SimpleArithmetic")]
    public object? NCalc_Compile_SimpleArithmetic() => _ncalcCompiledSimple!();

    [Benchmark(Description = "NCalc: Compile/复杂算术")]
    [BenchmarkCategory("NCalc", "Compile", "ComplexArithmetic")]
    public object? NCalc_Compile_ComplexArithmetic() => _ncalcCompiledComplex!();

    [Benchmark(Description = "NCalc: Compile/函数调用")]
    [BenchmarkCategory("NCalc", "Compile", "FunctionCall")]
    public object? NCalc_Compile_FunctionCall() => _ncalcCompiledFunction!();

    [Benchmark(Description = "NCalc: Compile/嵌套函数")]
    [BenchmarkCategory("NCalc", "Compile", "NestedFunction")]
    public object? NCalc_Compile_NestedFunction() => _ncalcCompiledNested!();

    [Benchmark(Description = "NCalc: Compile/三元运算")]
    [BenchmarkCategory("NCalc", "Compile", "Conditional")]
    public object? NCalc_Compile_Conditional() => _ncalcCompiledConditional!();

    [Benchmark(Description = "NCalc: Compile/逻辑运算")]
    [BenchmarkCategory("NCalc", "Compile", "Logical")]
    public object? NCalc_Compile_Logical() => _ncalcCompiledLogical!();

    [Benchmark(Description = "NCalc: Compile/对数")]
    [BenchmarkCategory("NCalc", "Compile", "Log")]
    public object? NCalc_Compile_Log() => _ncalcCompiledLog!();

    // ============================================================
    // NFun — Interpret
    // ============================================================

    [Benchmark(Description = "NFun: Interpret/简单算术")]
    [BenchmarkCategory("NFun", "Interpret", "SimpleArithmetic")]
    public object NFun_Interpret_SimpleArithmetic()
        => Funny.Calc<double>(NFunTransform(MakeUnique(SimpleArithmetic)));

    [Benchmark(Description = "NFun: Interpret/复杂算术")]
    [BenchmarkCategory("NFun", "Interpret", "ComplexArithmetic")]
    public object NFun_Interpret_ComplexArithmetic()
        => Funny.Calc<double>(NFunTransform(MakeUnique(ComplexArithmetic)));

    [Benchmark(Description = "NFun: Interpret/函数调用")]
    [BenchmarkCategory("NFun", "Interpret", "FunctionCall")]
    public object NFun_Interpret_FunctionCall()
        => Funny.Calc<double>(NFunTransform(MakeUnique(FunctionCall)));

    [Benchmark(Description = "NFun: Interpret/嵌套函数")]
    [BenchmarkCategory("NFun", "Interpret", "NestedFunction")]
    public object NFun_Interpret_NestedFunction()
        => Funny.Calc<double>(NFunTransform(MakeUnique(NestedFunction)));

    [Benchmark(Description = "NFun: Interpret/三元运算")]
    [BenchmarkCategory("NFun", "Interpret", "Conditional")]
    public object NFun_Interpret_Conditional()
        => Funny.Calc<double>(NFunTransform(MakeUnique(Conditional)));

    [Benchmark(Description = "NFun: Interpret/逻辑运算")]
    [BenchmarkCategory("NFun", "Interpret", "Logical")]
    public object NFun_Interpret_Logical()
        => Funny.Calc<double>(NFunTransform(MakeUnique(Logical)));

    [Benchmark(Description = "NFun: Interpret/对数")]
    [BenchmarkCategory("NFun", "Interpret", "Log")]
    public object NFun_Interpret_Log()
        => Funny.Calc<double>(NFunTransform(MakeUnique(LogExpression)));

    // ============================================================
    // NFun — Cached (BuildForCalcConstant)
    // ============================================================

    [Benchmark(Description = "NFun: Cached/简单算术")]
    [BenchmarkCategory("NFun", "Cached", "SimpleArithmetic")]
    public double NFun_Cached_SimpleArithmetic()
        => _nfunConstantCalc!.Calc(NFunTransform(SimpleArithmetic));

    [Benchmark(Description = "NFun: Cached/复杂算术")]
    [BenchmarkCategory("NFun", "Cached", "ComplexArithmetic")]
    public double NFun_Cached_ComplexArithmetic()
        => _nfunConstantCalc!.Calc(NFunTransform(ComplexArithmetic));

    [Benchmark(Description = "NFun: Cached/函数调用")]
    [BenchmarkCategory("NFun", "Cached", "FunctionCall")]
    public double NFun_Cached_FunctionCall()
        => _nfunConstantCalc!.Calc(NFunTransform(FunctionCall));

    [Benchmark(Description = "NFun: Cached/嵌套函数")]
    [BenchmarkCategory("NFun", "Cached", "NestedFunction")]
    public double NFun_Cached_NestedFunction()
        => _nfunConstantCalc!.Calc(NFunTransform(NestedFunction));

    [Benchmark(Description = "NFun: Cached/三元运算")]
    [BenchmarkCategory("NFun", "Cached", "Conditional")]
    public double NFun_Cached_Conditional()
        => _nfunConstantCalc!.Calc(NFunTransform(Conditional));

    [Benchmark(Description = "NFun: Cached/逻辑运算")]
    [BenchmarkCategory("NFun", "Cached", "Logical")]
    public double NFun_Cached_Logical()
        => _nfunConstantCalc!.Calc(NFunTransform(Logical));

    [Benchmark(Description = "NFun: Cached/对数")]
    [BenchmarkCategory("NFun", "Cached", "Log")]
    public double NFun_Cached_Log()
        => _nfunConstantCalc!.Calc(NFunTransform(LogExpression));

    // ============================================================
    // NFun — Compile (ToLambda)
    // ============================================================

    [Benchmark(Description = "NFun: Compile/简单算术")]
    [BenchmarkCategory("NFun", "Compile", "SimpleArithmetic")]
    public double NFun_Compile_SimpleArithmetic() => _nfunCompiledSimple!(new { });

    [Benchmark(Description = "NFun: Compile/复杂算术")]
    [BenchmarkCategory("NFun", "Compile", "ComplexArithmetic")]
    public double NFun_Compile_ComplexArithmetic() => _nfunCompiledComplex!(new { });

    [Benchmark(Description = "NFun: Compile/函数调用")]
    [BenchmarkCategory("NFun", "Compile", "FunctionCall")]
    public double NFun_Compile_FunctionCall() => _nfunCompiledFunction!(new { });

    [Benchmark(Description = "NFun: Compile/嵌套函数")]
    [BenchmarkCategory("NFun", "Compile", "NestedFunction")]
    public double NFun_Compile_NestedFunction() => _nfunCompiledNested!(new { });

    [Benchmark(Description = "NFun: Compile/三元运算")]
    [BenchmarkCategory("NFun", "Compile", "Conditional")]
    public double NFun_Compile_Conditional() => _nfunCompiledConditional!(new { });

    [Benchmark(Description = "NFun: Compile/逻辑运算")]
    [BenchmarkCategory("NFun", "Compile", "Logical")]
    public double NFun_Compile_Logical() => _nfunCompiledLogical!(new { });

    [Benchmark(Description = "NFun: Compile/对数")]
    [BenchmarkCategory("NFun", "Compile", "Log")]
    public double NFun_Compile_Log() => _nfunCompiledLog!(new { });

    // ============================================================
    // MathEvaluator — Interpret
    // 注意：MathEvaluator 不支持 ? : 三元运算和 and 逻辑运算，跳过 Conditional/Logical
    // ============================================================

    [Benchmark(Description = "MathEvaluator: Interpret/简单算术")]
    [BenchmarkCategory("MathEvaluator", "Interpret", "SimpleArithmetic")]
    public double MathEvaluator_Interpret_SimpleArithmetic()
        => MakeUnique(SimpleArithmetic).Evaluate(_mathEvalCtx2);

    [Benchmark(Description = "MathEvaluator: Interpret/复杂算术")]
    [BenchmarkCategory("MathEvaluator", "Interpret", "ComplexArithmetic")]
    public double MathEvaluator_Interpret_ComplexArithmetic()
        => MakeUnique(ComplexArithmetic).Evaluate(_mathEvalCtx2);

    [Benchmark(Description = "MathEvaluator: Interpret/函数调用")]
    [BenchmarkCategory("MathEvaluator", "Interpret", "FunctionCall")]
    public double MathEvaluator_Interpret_FunctionCall()
        => MakeUnique(FunctionCall).Evaluate(_mathEvalCtx2);

    [Benchmark(Description = "MathEvaluator: Interpret/嵌套函数")]
    [BenchmarkCategory("MathEvaluator", "Interpret", "NestedFunction")]
    public double MathEvaluator_Interpret_NestedFunction()
        => MakeUnique(NestedFunction).Evaluate(_mathEvalCtx2);

    // Conditional: MathEvaluator 不支持 ? : 三元运算，跳过

    // Logical: MathEvaluator 不支持 and 逻辑运算，跳过

    [Benchmark(Description = "MathEvaluator: Interpret/对数")]
    [BenchmarkCategory("MathEvaluator", "Interpret", "Log")]
    public double MathEvaluator_Interpret_Log()
        => MathEvaluatorTransform(MakeUnique(LogExpression)).Evaluate(_mathEvalCtx2);

    // ============================================================
    // MathEvaluator — Compile
    // 注意：MathEvaluator 不支持 ? : 三元运算和 and 逻辑运算，跳过 Conditional/Logical
    // ============================================================

    [Benchmark(Description = "MathEvaluator: Compile/简单算术")]
    [BenchmarkCategory("MathEvaluator", "Compile", "SimpleArithmetic")]
    public double MathEvaluator_Compile_SimpleArithmetic() => (double)_mathEvalCompiledSimple!.DynamicInvoke(new { })!;

    [Benchmark(Description = "MathEvaluator: Compile/复杂算术")]
    [BenchmarkCategory("MathEvaluator", "Compile", "ComplexArithmetic")]
    public double MathEvaluator_Compile_ComplexArithmetic() => (double)_mathEvalCompiledComplex!.DynamicInvoke(new { })!;

    [Benchmark(Description = "MathEvaluator: Compile/函数调用")]
    [BenchmarkCategory("MathEvaluator", "Compile", "FunctionCall")]
    public double MathEvaluator_Compile_FunctionCall() => (double)_mathEvalCompiledFunction!.DynamicInvoke(new { })!;

    [Benchmark(Description = "MathEvaluator: Compile/嵌套函数")]
    [BenchmarkCategory("MathEvaluator", "Compile", "NestedFunction")]
    public double MathEvaluator_Compile_NestedFunction() => (double)_mathEvalCompiledNested!.DynamicInvoke(new { })!;

    // Conditional: MathEvaluator 不支持 ? : 三元运算，跳过

    // Logical: MathEvaluator 不支持 and 逻辑运算，跳过

    [Benchmark(Description = "MathEvaluator: Compile/对数")]
    [BenchmarkCategory("MathEvaluator", "Compile", "Log")]
    public double MathEvaluator_Compile_Log() => (double)_mathEvalCompiledLog!.DynamicInvoke(new { })!;
}

/// <summary>
/// 自定义 BenchmarkDotNet 导出器：生成 3D 可交互可视化 HTML 报告
/// </summary>
public class HtmlReportExporter : IExporter {
    public string Name => "Html3DReport";

    public void ExportToLog(BenchmarkDotNet.Reports.Summary summary, ILogger logger) {
        var filePath = Path.Combine(summary.ResultsDirectoryPath, "report.html");
        ExportToFile(summary, filePath);
    }

    public IEnumerable<string> ExportToFiles(BenchmarkDotNet.Reports.Summary summary, ILogger logger) {
        var filePath = Path.Combine(summary.ResultsDirectoryPath, "report.html");
        ExportToFile(summary, filePath);
        return [filePath];
    }

    private void ExportToFile(BenchmarkDotNet.Reports.Summary summary, string filePath) {
        var dataPoints = new List<string>();
        foreach (var report in summary.Reports) {
            var benchmark = report.BenchmarkCase;
            var desc = benchmark.Descriptor;
            var categories = benchmark.Descriptor.Categories;

            var library = categories.FirstOrDefault(c => c is "MathEval" or "MathEval.Fast" or "NCalc" or "NFun" or "MathEvaluator") ?? "Unknown";
            var mode = categories.FirstOrDefault(c => c is "Interpret" or "Cached" or "Compile") ?? "Unknown";
            var expression = categories.FirstOrDefault(c => c is "SimpleArithmetic" or "ComplexArithmetic" or "FunctionCall" or "NestedFunction" or "Conditional" or "Logical" or "Log") ?? "Unknown";

            var mean = report.ResultStatistics?.Mean ?? 0;
            var meanUs = mean / 1000.0; // ns -> μs
            var stdErr = report.ResultStatistics?.StandardError ?? 0;
            var stdErrUs = stdErr / 1000.0;
            var allocated = report.GcStats.GetTotalAllocatedBytes(true);

            dataPoints.Add($"{{library:\"{library}\",mode:\"{mode}\",expression:\"{expression}\",mean:{meanUs.ToString("F4", CultureInfo.InvariantCulture)},stdErr:{stdErrUs.ToString("F4", CultureInfo.InvariantCulture)},allocated:{allocated}}}");
        }

        var dataJson = $"[{string.Join(",", dataPoints)}]";

        var html = GenerateHtml(dataJson);
        File.WriteAllText(filePath, html);
    }

    private static string GenerateHtml(string dataJson) {
        return $$"""
<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>MathEval 三维性能对比报告</title>
<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
body { background: #ffffff; color: #1a1a2e; font-family: 'Segoe UI', system-ui, sans-serif; overflow: hidden; }
#container { width: 100vw; height: 100vh; }
#controls { position: fixed; top: 16px; left: 16px; z-index: 10; background: rgba(255,255,255,0.92); border-radius: 12px; padding: 16px; min-width: 240px; backdrop-filter: blur(8px); border: 1px solid rgba(0,0,0,0.08); box-shadow: 0 2px 12px rgba(0,0,0,0.08); }
#controls h2 { font-size: 16px; margin-bottom: 12px; color: #4338ca; }
#controls label { display: block; margin: 6px 0; font-size: 13px; cursor: pointer; color: #333; }
#controls input[type="checkbox"] { margin-right: 6px; }
#tooltip { position: fixed; display: none; background: rgba(255,255,255,0.96); border: 1px solid rgba(67,56,202,0.3); border-radius: 8px; padding: 12px; font-size: 13px; z-index: 20; pointer-events: none; backdrop-filter: blur(8px); max-width: 300px; box-shadow: 0 2px 12px rgba(0,0,0,0.1); color: #1a1a2e; }
#tooltip .tt-title { color: #4338ca; font-weight: 600; margin-bottom: 4px; }
#tooltip .tt-row { margin: 2px 0; }
#legend { position: fixed; top: 16px; right: 16px; z-index: 10; background: rgba(255,255,255,0.92); border-radius: 12px; padding: 16px; backdrop-filter: blur(8px); border: 1px solid rgba(0,0,0,0.08); box-shadow: 0 2px 12px rgba(0,0,0,0.08); }
#legend h3 { font-size: 14px; margin-bottom: 8px; color: #4338ca; }
.legend-item { display: flex; align-items: center; gap: 8px; margin: 4px 0; font-size: 13px; color: #333; }
.legend-color { width: 14px; height: 14px; border-radius: 3px; }
#info { position: fixed; bottom: 16px; left: 50%; transform: translateX(-50%); z-index: 10; background: rgba(255,255,255,0.92); border-radius: 8px; padding: 8px 16px; font-size: 12px; color: #888; backdrop-filter: blur(8px); border: 1px solid rgba(0,0,0,0.08); box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
</style>
</head>
<body>
<div id="controls">
  <h2>显示控制</h2>
  <div id="mode-filters"></div>
</div>
<div id="legend">
  <h3>图例</h3>
  <div id="legend-items"></div>
</div>
<div id="views" style="position:fixed;top:16px;left:50%;transform:translateX(-50%);z-index:10;display:flex;gap:6px;">
  <button onclick="setView('front')" style="padding:6px 14px;border:1px solid rgba(0,0,0,0.1);border-radius:6px;background:rgba(255,255,255,0.92);cursor:pointer;font-size:13px;color:#333;backdrop-filter:blur(8px);">正视图</button>
  <button onclick="setView('top')" style="padding:6px 14px;border:1px solid rgba(0,0,0,0.1);border-radius:6px;background:rgba(255,255,255,0.92);cursor:pointer;font-size:13px;color:#333;backdrop-filter:blur(8px);">俯视图</button>
  <button onclick="setView('side')" style="padding:6px 14px;border:1px solid rgba(0,0,0,0.1);border-radius:6px;background:rgba(255,255,255,0.92);cursor:pointer;font-size:13px;color:#333;backdrop-filter:blur(8px);">侧视图</button>
  <button onclick="setView('iso')" style="padding:6px 14px;border:1px solid rgba(0,0,0,0.1);border-radius:6px;background:rgba(255,255,255,0.92);cursor:pointer;font-size:13px;color:#333;backdrop-filter:blur(8px);">等轴测</button>
</div>
<div id="tooltip"></div>
<div id="container"></div>
<div id="info">鼠标拖拽旋转 | 滚轮缩放 | 悬停柱体查看详情</div>

<script src="https://cdn.jsdelivr.net/npm/three@0.150.0/build/three.min.js"></script>
<script>
// Z-up OrbitControls — 用方位角+仰角，兼容 Z 朝上
THREE.ZUpOrbitControls = function(camera, domElement) {
  var azimuth = Math.atan2(camera.position.y - 0, camera.position.x - 0);
  var elevation = Math.atan2(camera.position.z - 0, Math.sqrt(camera.position.x * camera.position.x + camera.position.y * camera.position.y));
  var distance = camera.position.length();
  var target = new THREE.Vector3(10, 4, 2);
  var state = { rotating: false, panning: false, prevX: 0, prevY: 0 };

  this.target = target;
  this.enableDamping = true;
  this.dampingFactor = 0.1;
  this.rotateSpeed = 0.8;
  this.panSpeed = 0.4;
  this.minDistance = 5;
  this.maxDistance = 80;

  var dampAz = azimuth, dampEl = elevation, dampDist = distance;

  this.update = function() {
    if (this.enableDamping) {
      dampAz += (azimuth - dampAz) * this.dampingFactor;
      dampEl += (elevation - dampEl) * this.dampingFactor;
      dampDist += (distance - dampDist) * this.dampingFactor;
    } else {
      dampAz = azimuth; dampEl = elevation; dampDist = distance;
    }
    var a = dampAz, e = dampEl, d = dampDist;
    camera.position.set(
      target.x + d * Math.cos(e) * Math.cos(a),
      target.y + d * Math.cos(e) * Math.sin(a),
      target.z + d * Math.sin(e)
    );
    camera.lookAt(target);
  };

  domElement.addEventListener('mousedown', function(e) {
    if (e.button === 0) state.rotating = true;
    if (e.button === 2) state.panning = true;
    state.prevX = e.clientX; state.prevY = e.clientY;
  });
  domElement.addEventListener('contextmenu', function(e) { e.preventDefault(); });
  window.addEventListener('mouseup', function() { state.rotating = false; state.panning = false; });
  window.addEventListener('mousemove', function(e) {
    var dx = e.clientX - state.prevX, dy = e.clientY - state.prevY;
    state.prevX = e.clientX; state.prevY = e.clientY;
    if (state.rotating) {
      azimuth -= dx * 0.008 * this.rotateSpeed;
      elevation += dy * 0.008 * this.rotateSpeed;
      elevation = Math.max(-Math.PI * 0.45, Math.min(Math.PI * 0.45, elevation));
    }
    if (state.panning) {
      var right = new THREE.Vector3();
      right.setFromMatrixColumn(camera.matrixWorld, 0); // camera right
      var up = new THREE.Vector3(0, 0, 1); // Z-up pan
      target.add(right.multiplyScalar(-dx * 0.02 * this.panSpeed));
      target.add(up.multiplyScalar(dy * 0.02 * this.panSpeed));
    }
  }.bind(this));
  domElement.addEventListener('wheel', function(e) {
    e.preventDefault();
    distance *= e.deltaY > 0 ? 1.08 : 0.93;
    distance = Math.max(this.minDistance, Math.min(this.maxDistance, distance));
  }.bind(this), { passive: false });

  // 暴露 setter 供视图切换使用
  this.setSpherical = function(az, el, dist) {
    azimuth = az; elevation = el; distance = dist;
    dampAz = az; dampEl = el; dampDist = dist;
  };
};

// 视图切换（全局函数）
var _viewCamera, _viewControls;
function setView(view) {
  if (!_viewControls) return;
  var cx = 10, cy = 4, cz = 2;
  var d = 25;
  var az, el, dist = d;
  switch(view) {
    case 'front': az = -Math.PI / 2; el = 0; break;
    case 'top':   az = 0; el = Math.PI * 0.45; break;
    case 'side':  az = Math.PI; el = 0; break;
    case 'iso':   az = -0.9; el = 0.6; dist = 28; break;
  }
  _viewControls.target.set(cx, cy, cz);
  _viewControls.setSpherical(az, el, dist);
}
</script>
<script>
(function() {
var DATA = {dataJson};

var LIBRARIES = ['MathEval', 'MathEval.Fast', 'NCalc', 'NFun', 'MathEvaluator'];
var MODES = ['Interpret', 'Cached', 'Compile'];
var EXPRESSIONS = ['SimpleArithmetic', 'ComplexArithmetic', 'FunctionCall', 'NestedFunction', 'Conditional', 'Logical', 'Log'];
var EXPR_LABELS = ['简单算术', '复杂算术', '函数调用', '嵌套函数', '三元运算', '逻辑运算', '对数运算'];

var COLORS = {
  'MathEval': 0x6366f1,
  'MathEval.Fast': 0x06b6d4,
  'NCalc': 0xf59e0b,
  'NFun': 0xef4444,
  'MathEvaluator': 0x10b981
};

var COLOR_HEX = {
  'MathEval': '#6366f1',
  'MathEval.Fast': '#06b6d4',
  'NCalc': '#f59e0b',
  'NFun': '#ef4444',
  'MathEvaluator': '#10b981'
};

// 场景初始化 — 亮色主题，坐标轴: X右=表达式, Y前=模式, Z上=时间
var scene = new THREE.Scene();
scene.background = new THREE.Color(0xffffff);
scene.fog = new THREE.Fog(0xffffff, 40, 80);

var camera = new THREE.PerspectiveCamera(50, window.innerWidth / window.innerHeight, 0.1, 100);
camera.position.set(14, -18, 14);
camera.up.set(0, 0, 1);

var renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.setPixelRatio(Math.devicePixelRatio);
renderer.shadowMap.enabled = true;
document.getElementById('container').appendChild(renderer.domElement);

var controls = new THREE.ZUpOrbitControls(camera, renderer.domElement);
_viewCamera = camera;
_viewControls = controls;

// 灯光
var ambientLight = new THREE.AmbientLight(0xffffff, 1.8);
scene.add(ambientLight);
var dirLight = new THREE.DirectionalLight(0xffffff, 1.0);
dirLight.position.set(10, -10, 20);
dirLight.castShadow = true;
scene.add(dirLight);

// 坐标轴标签
function createTextSprite(text, color, size) {
  color = color || '#aaa';
  size = size || 0.5;
  var dpr = Math.min(window.devicePixelRatio || 1, 3);
  var cw = 1024, ch = 256;
  var canvas = document.createElement('canvas');
  canvas.width = cw * dpr;
  canvas.height = ch * dpr;
  var ctx = canvas.getContext('2d');
  ctx.scale(dpr, dpr);
  ctx.font = 'bold 112px "Segoe UI", Arial, sans-serif';
  ctx.fillStyle = color;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.imageSmoothingEnabled = true;
  ctx.fillText(text, cw / 2, ch / 2);
  var tex = new THREE.CanvasTexture(canvas);
  tex.minFilter = THREE.LinearFilter;
  tex.magFilter = THREE.LinearFilter;
  var mat = new THREE.SpriteMaterial({ map: tex, transparent: true, sizeAttenuation: true });
  var sprite = new THREE.Sprite(mat);
  sprite.scale.set(size * 4, size, 1);
  return sprite;
}

// X轴标签（表达式类型）— X向右
EXPR_LABELS.forEach(function(label, i) {
  var sprite = createTextSprite(label, '#4338ca', 1.2);
  sprite.position.set(i * 3.5 + 1, -2, -1.5);
  scene.add(sprite);
});

// Y轴标签（模式）— Y向前
MODES.forEach(function(mode, i) {
  var sprite = createTextSprite(mode, '#555', 1.2);
  sprite.position.set(-2, i * 3 + 1, -1.5);
  scene.add(sprite);
});

// Z轴标签（时间）— Z向上
var zLabel = createTextSprite('执行时间 (μs)', '#555', 1.5);
zLabel.position.set(-3, 0, 5);
scene.add(zLabel);

// 构建3D柱体
var bars = [];
var barGroup = new THREE.Group();
scene.add(barGroup);

var visibleModes = new Set(MODES);
var visibleLibs = new Set(LIBRARIES);
var showValues = false;

function buildBars() {
  // 清除旧柱体
  while (barGroup.children.length) barGroup.remove(barGroup.children[0]);
  bars.length = 0;

  // 计算最大值用于缩放
  var visibleData = DATA.filter(function(d) { return visibleModes.has(d.mode) && visibleLibs.has(d.library); });
  var maxMean = Math.max.apply(null, visibleData.map(function(d) { return d.mean; }).concat([0.001]));
  var scaleZ = 8 / maxMean;

  DATA.forEach(function(d) {
    if (!visibleModes.has(d.mode) || !visibleLibs.has(d.library)) return;

    var exprIdx = EXPRESSIONS.indexOf(d.expression);
    var modeIdx = MODES.indexOf(d.mode);
    var libIdx = LIBRARIES.indexOf(d.library);

    var height = Math.max(d.mean * scaleZ, 0.05);
    var geometry = new THREE.BoxGeometry(0.45, 0.45, height);
    var material = new THREE.MeshPhongMaterial({
      color: COLORS[d.library],
      shininess: 60
    });
    var mesh = new THREE.Mesh(geometry, material);

    var x = exprIdx * 3.5 + libIdx * 0.55 + 0.3;
    var y = modeIdx * 3 + 1;
    mesh.position.set(x, y, height / 2);
    mesh.castShadow = true;
    mesh.receiveShadow = true;

    mesh.userData = { library: d.library, mode: d.mode, expression: d.expression, mean: d.mean, stdErr: d.stdErr, allocated: d.allocated, height: height };
    bars.push(mesh);
    barGroup.add(mesh);

    // 柱顶数值标签（按配置显示）
    if (showValues) {
      var valText = d.mean < 1 ? d.mean.toFixed(3) : d.mean < 10 ? d.mean.toFixed(2) : d.mean.toFixed(1);
      var valSprite = createTextSprite(valText, COLOR_HEX[d.library], 0.6);
      valSprite.position.set(x, y, height + 0.5);
      barGroup.add(valSprite);
    }
  });
}

buildBars();

// 图例
var legendDiv = document.getElementById('legend-items');
LIBRARIES.forEach(function(lib) {
  var item = document.createElement('div');
  item.className = 'legend-item';
  item.innerHTML = '<div class="legend-color" style="background:' + COLOR_HEX[lib] + '"></div>' + lib;
  legendDiv.appendChild(item);
});

// 控制面板
var modeFilters = document.getElementById('mode-filters');
MODES.forEach(function(mode) {
  var label = document.createElement('label');
  label.innerHTML = '<input type="checkbox" checked data-mode="' + mode + '"> ' + mode;
  label.querySelector('input').addEventListener('change', function(e) {
    if (e.target.checked) visibleModes.add(mode); else visibleModes.delete(mode);
    buildBars();
  });
  modeFilters.appendChild(label);
});

var libFilters = document.createElement('div');
libFilters.style.marginTop = '12px';
LIBRARIES.forEach(function(lib) {
  var label = document.createElement('label');
  label.innerHTML = '<input type="checkbox" checked data-lib="' + lib + '"> ' + lib;
  label.querySelector('input').addEventListener('change', function(e) {
    if (e.target.checked) visibleLibs.add(lib); else visibleLibs.delete(lib);
    buildBars();
  });
  libFilters.appendChild(label);
});
modeFilters.appendChild(libFilters);

// 数值显示勾选框
var valLabel = document.createElement('label');
valLabel.style.marginTop = '12px';
valLabel.style.fontWeight = '600';
valLabel.innerHTML = '<input type="checkbox" id="showValues"> 显示柱顶数值';
valLabel.querySelector('input').addEventListener('change', function(e) {
  showValues = e.target.checked;
  buildBars();
});
modeFilters.appendChild(valLabel);

// 鼠标悬停
var raycaster = new THREE.Raycaster();
var mouse = new THREE.Vector2();
var tooltip = document.getElementById('tooltip');

renderer.domElement.addEventListener('mousemove', function(e) {
  mouse.x = (e.clientX / window.innerWidth) * 2 - 1;
  mouse.y = -(e.clientY / window.innerHeight) * 2 + 1;

  raycaster.setFromCamera(mouse, camera);
  var intersects = raycaster.intersectObjects(bars);

  if (intersects.length > 0) {
    var d = intersects[0].object.userData;
    tooltip.style.display = 'block';
    tooltip.style.left = (e.clientX + 16) + 'px';
    tooltip.style.top = (e.clientY + 16) + 'px';
    tooltip.innerHTML =
      '<div class="tt-title">' + d.library + ' — ' + d.mode + '</div>' +
      '<div class="tt-row">表达式: ' + d.expression + '</div>' +
      '<div class="tt-row">均值: ' + d.mean.toFixed(4) + ' μs</div>' +
      '<div class="tt-row">标准误差: ±' + d.stdErr.toFixed(4) + ' μs</div>' +
      '<div class="tt-row">内存分配: ' + (d.allocated / 1024).toFixed(1) + ' KB</div>';
    intersects[0].object.material.emissive = new THREE.Color(COLORS[d.library]).multiplyScalar(0.2);
  } else {
    tooltip.style.display = 'none';
    bars.forEach(function(b) { b.material.emissive = new THREE.Color(0x000000); });
  }
});

// 动画循环
function animate() {
  requestAnimationFrame(animate);
  controls.update();
  renderer.render(scene, camera);
}
animate();

// 响应窗口大小变化
window.addEventListener('resize', function() {
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
});
})();
</script>
</body>
</html>
""";
    }
}
