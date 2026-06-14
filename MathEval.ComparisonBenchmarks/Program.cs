using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using MathEval.Fast;
using MathEvaluation.Context;
using MathEvaluation.Extensions;
using NCalc;
using NFun;
using System.Text.RegularExpressions;

BenchmarkRunner.Run<ComparisonBenchmarks>(args: args);

[MemoryDiagnoser]
[RankColumn]
public partial class ComparisonBenchmarks {
    // 测试表达式
    private const string SimpleArithmetic = "1 + 2 * 3 - 4 / 2";
    private const string ComplexArithmetic = "(3.14 + 2.72) * (1.41 - 0.58) / (3.67 + 1.23) + 2 ^ 3";
    private const string FunctionCall = "sin(0.5) + cos(0.3) * sqrt(16) - abs(-5)";
    private const string NestedFunction = "sqrt(pow(sin(0.5), 2) + pow(cos(0.5), 2))";
    private const string Conditional = "1 > 0 ? 42 : -1";
    private const string Logical = "1 > 0 and 2 < 3 ? 100 : 0";
    private const string LogExpression = "log(100) + ln(2.718) + log(8, 2)";

    // MathEval 上下文
    private MathEval.Context.ExpressionContext? _mathEvalContext;

    // MathEvaluator 上下文
    private readonly MathContext _mathEvaluatorContext = new ScientificMathContext();

    // NCalc 上下文（v6 使用 ExpressionContext）
    private readonly ExpressionContext _ncalcContext = new() {
        Options = ExpressionOptions.IgnoreCaseAtBuiltInFunctions | ExpressionOptions.NoCache
    };

    // 用于生成唯一表达式的计数器
    private int _counter;

    [GlobalSetup]
    public void Setup() {
        _mathEvalContext = new MathEval.Context.ExpressionContext();
    }

    [IterationSetup]
    public void IterationSetup() => _counter = 0;

    private string MakeUnique(string expr) {
        _counter++;
        return expr.Replace("0.5", (0.5 + _counter * 0.0001).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // ============================================================
    // 简单算术表达式测试
    // ============================================================

    [Benchmark(Description = "MathEval: 简单算术")]
    [BenchmarkCategory("SimpleArithmetic")]
    public object? MathEval_SimpleArithmetic() {
        return MathEval.Expression.Eval(MakeUnique(SimpleArithmetic), _mathEvalContext, MathEval.ExpressionOptions.NoCache);
    }

    [Benchmark(Description = "MathEval.Fast: 简单算术")]
    [BenchmarkCategory("SimpleArithmetic")]
    public double FastEval_SimpleArithmetic() {
        return FastEval.EvalDouble(MakeUnique(SimpleArithmetic));
    }

    [Benchmark(Description = "NFun: 简单算术")]
    [BenchmarkCategory("SimpleArithmetic")]
    public object NFun_SimpleArithmetic() {
        // NFun 使用 ** 作为幂运算符
        var expr = MakeUnique(SimpleArithmetic).Replace("^", "**");
        return Funny.Calc<double>(expr);
    }

    [Benchmark(Description = "NCalc: 简单算术")]
    [BenchmarkCategory("SimpleArithmetic")]
    public object? NCalc_SimpleArithmetic() {
        var expr = new Expression(MakeUnique(SimpleArithmetic), _ncalcContext);
        return expr.Evaluate();
    }

    [Benchmark(Description = "MathEvaluator: 简单算术")]
    [BenchmarkCategory("SimpleArithmetic")]
    public double MathEvaluator_SimpleArithmetic() {
        return MakeUnique(SimpleArithmetic).Evaluate(_mathEvaluatorContext);
    }

    // ============================================================
    // 复杂算术表达式测试
    // ============================================================

    [Benchmark(Description = "MathEval: 复杂算术")]
    [BenchmarkCategory("ComplexArithmetic")]
    public object? MathEval_ComplexArithmetic() {
        return MathEval.Expression.Eval(MakeUnique(ComplexArithmetic), _mathEvalContext, MathEval.ExpressionOptions.NoCache);
    }

    [Benchmark(Description = "MathEval.Fast: 复杂算术")]
    [BenchmarkCategory("ComplexArithmetic")]
    public double FastEval_ComplexArithmetic() {
        return FastEval.EvalDouble(MakeUnique(ComplexArithmetic));
    }

    [Benchmark(Description = "NFun: 复杂算术")]
    [BenchmarkCategory("ComplexArithmetic")]
    public object NFun_ComplexArithmetic() {
        var expr = MakeUnique(ComplexArithmetic).Replace("^", "**");
        return Funny.Calc<double>(expr);
    }

    [Benchmark(Description = "NCalc: 复杂算术")]
    [BenchmarkCategory("ComplexArithmetic")]
    public object? NCalc_ComplexArithmetic() {
        var expr = new Expression(MakeUnique(ComplexArithmetic), _ncalcContext);
        return expr.Evaluate();
    }

    [Benchmark(Description = "MathEvaluator: 复杂算术")]
    [BenchmarkCategory("ComplexArithmetic")]
    public double MathEvaluator_ComplexArithmetic() {
        return MakeUnique(ComplexArithmetic).Evaluate(_mathEvaluatorContext);
    }

    // ============================================================
    // 函数调用测试
    // ============================================================

    [Benchmark(Description = "MathEval: 函数调用")]
    [BenchmarkCategory("FunctionCall")]
    public object? MathEval_FunctionCall() {
        return MathEval.Expression.Eval(MakeUnique(FunctionCall), _mathEvalContext, MathEval.ExpressionOptions.NoCache);
    }

    [Benchmark(Description = "MathEval.Fast: 函数调用")]
    [BenchmarkCategory("FunctionCall")]
    public double FastEval_FunctionCall() {
        return FastEval.EvalDouble(MakeUnique(FunctionCall));
    }

    [Benchmark(Description = "NFun: 函数调用")]
    [BenchmarkCategory("FunctionCall")]
    public object NFun_FunctionCall() {
        var expr = MakeUnique(FunctionCall);
        return Funny.Calc<double>(expr);
    }

    [Benchmark(Description = "NCalc: 函数调用")]
    [BenchmarkCategory("FunctionCall")]
    public object? NCalc_FunctionCall() {
        // NCalc v6 内置 Sin, Cos, Sqrt, Abs 等数学函数（IgnoreCaseAtBuiltInFunctions 支持小写）
        var expr = new Expression(MakeUnique(FunctionCall), _ncalcContext);
        return expr.Evaluate();
    }

    [Benchmark(Description = "MathEvaluator: 函数调用")]
    [BenchmarkCategory("FunctionCall")]
    public double MathEvaluator_FunctionCall() {
        return MakeUnique(FunctionCall).Evaluate(_mathEvaluatorContext);
    }

    // ============================================================
    // 嵌套函数测试
    // ============================================================

    [Benchmark(Description = "MathEval: 嵌套函数")]
    [BenchmarkCategory("NestedFunction")]
    public object? MathEval_NestedFunction() {
        return MathEval.Expression.Eval(MakeUnique(NestedFunction), _mathEvalContext, MathEval.ExpressionOptions.NoCache);
    }

    [Benchmark(Description = "MathEval.Fast: 嵌套函数")]
    [BenchmarkCategory("NestedFunction")]
    public double FastEval_NestedFunction() {
        return FastEval.EvalDouble(MakeUnique(NestedFunction));
    }

    [Benchmark(Description = "NFun: 嵌套函数")]
    [BenchmarkCategory("NestedFunction")]
    public object NFun_NestedFunction() {
        // NFun: pow(x, y) -> x ** y
        var expr = MakeUnique(NestedFunction);
        // 替换 pow(sin(x), 2) 为 (sin(x)**2)
        expr = MyRegex().Replace(expr, m => {
            var inner = m.Value.Replace("pow(", "").Replace(", 2)", "");
            return $"({inner}**2)";
        });
        expr = MyRegex1().Replace(expr, m => {
            var inner = m.Value.Replace("pow(", "").Replace(", 2)", "");
            return $"({inner}**2)";
        });
        return Funny.Calc<double>(expr);
    }

    [Benchmark(Description = "NCalc: 嵌套函数")]
    [BenchmarkCategory("NestedFunction")]
    public object? NCalc_NestedFunction() {
        // NCalc v6 内置 Sqrt, Pow, Sin, Cos 等函数
        var expr = new Expression(MakeUnique(NestedFunction), _ncalcContext);
        return expr.Evaluate();
    }

    [Benchmark(Description = "MathEvaluator: 嵌套函数")]
    [BenchmarkCategory("NestedFunction")]
    public double MathEvaluator_NestedFunction() {
        return MakeUnique(NestedFunction).Evaluate(_mathEvaluatorContext);
    }

    // ============================================================
    // 三元运算测试
    // ============================================================

    [Benchmark(Description = "MathEval: 三元运算")]
    [BenchmarkCategory("Conditional")]
    public object? MathEval_Conditional() {
        return MathEval.Expression.Eval(MakeUnique(Conditional), _mathEvalContext, MathEval.ExpressionOptions.NoCache);
    }

    [Benchmark(Description = "MathEval.Fast: 三元运算")]
    [BenchmarkCategory("Conditional")]
    public double FastEval_Conditional() {
        return FastEval.EvalDouble(MakeUnique(Conditional));
    }

    [Benchmark(Description = "NFun: 三元运算")]
    [BenchmarkCategory("Conditional")]
    public object NFun_Conditional() {
        // NFun 使用 if condition then trueVal else falseVal 表达式
        var expr = MakeUnique(Conditional);
        var parts = expr.Split('?');
        var condition = parts[0].Trim();
        var rest = parts[1].Split(':');
        var trueVal = rest[0].Trim();
        var falseVal = rest[1].Trim();
        return Funny.Calc<double>($"if {condition} then {trueVal} else {falseVal}");
    }

    [Benchmark(Description = "NCalc: 三元运算")]
    [BenchmarkCategory("Conditional")]
    public object? NCalc_Conditional() {
        // NCalc v6 原生支持三元运算符 ? :
        var expr = new Expression(MakeUnique(Conditional), _ncalcContext);
        return expr.Evaluate();
    }

    [Benchmark(Description = "MathEvaluator: 三元运算")]
    [BenchmarkCategory("Conditional")]
    public double MathEvaluator_Conditional() {
        return MakeUnique(Conditional).Evaluate(_mathEvaluatorContext);
    }

    // ============================================================
    // 逻辑运算测试
    // ============================================================

    [Benchmark(Description = "MathEval: 逻辑运算")]
    [BenchmarkCategory("Logical")]
    public object? MathEval_Logical() {
        return MathEval.Expression.Eval(MakeUnique(Logical), _mathEvalContext, MathEval.ExpressionOptions.NoCache);
    }

    [Benchmark(Description = "MathEval.Fast: 逻辑运算")]
    [BenchmarkCategory("Logical")]
    public double FastEval_Logical() {
        return FastEval.EvalDouble(MakeUnique(Logical));
    }

    [Benchmark(Description = "NFun: 逻辑运算")]
    [BenchmarkCategory("Logical")]
    public object NFun_Logical() {
        // NFun 使用 if condition then trueVal else falseVal 表达式
        var expr = MakeUnique(Logical);
        var parts = expr.Split('?');
        var condition = parts[0].Trim();
        var rest = parts[1].Split(':');
        var trueVal = rest[0].Trim();
        var falseVal = rest[1].Trim();
        return Funny.Calc<double>($"if {condition} then {trueVal} else {falseVal}");
    }

    [Benchmark(Description = "NCalc: 逻辑运算")]
    [BenchmarkCategory("Logical")]
    public object? NCalc_Logical() {
        // NCalc v6 支持 and/or 逻辑运算符和三元表达式
        var expr = new Expression(MakeUnique(Logical), _ncalcContext);
        return expr.Evaluate();
    }

    [Benchmark(Description = "MathEvaluator: 逻辑运算")]
    [BenchmarkCategory("Logical")]
    public double MathEvaluator_Logical() {
        return MakeUnique(Logical).Evaluate(_mathEvaluatorContext);
    }

    // ============================================================
    // 对数运算测试
    // ============================================================

    [Benchmark(Description = "MathEval: 对数")]
    [BenchmarkCategory("Log")]
    public object? MathEval_Log() {
        return MathEval.Expression.Eval(MakeUnique(LogExpression), _mathEvalContext, MathEval.ExpressionOptions.NoCache);
    }

    [Benchmark(Description = "MathEval.Fast: 对数")]
    [BenchmarkCategory("Log")]
    public double FastEval_Log() {
        return FastEval.EvalDouble(MakeUnique(LogExpression));
    }

    [Benchmark(Description = "NFun: 对数")]
    [BenchmarkCategory("Log")]
    public object NFun_Log() {
        // NFun: log10(x) 是常用对数，log(x) 是自然对数
        var expr = MakeUnique(LogExpression);
        // log(100) -> log10(100), ln -> log, log(8,2) -> log(8)/log(2)
        expr = expr.Replace("log(100)", "log10(100)");
        expr = expr.Replace("ln(", "log(");
        expr = expr.Replace("log(8, 2)", "(log(8)/log(2))");
        return Funny.Calc<double>(expr);
    }

    [Benchmark(Description = "NCalc: 对数")]
    [BenchmarkCategory("Log")]
    public object? NCalc_Log() {
        // NCalc v6 内置 Log(x, base), Ln(x), Log10(x) 函数
        var expr = new Expression(MakeUnique(LogExpression), _ncalcContext);
        return expr.Evaluate();
    }

    [Benchmark(Description = "MathEvaluator: 对数")]
    [BenchmarkCategory("Log")]
    public double MathEvaluator_Log() {
        return MakeUnique(LogExpression).Evaluate(_mathEvaluatorContext);
    }

    [GeneratedRegex("pow\\(sin\\([^)]+\\), 2\\)")]
    private static partial Regex MyRegex();
    [GeneratedRegex("pow\\(cos\\([^)]+\\), 2\\)")]
    private static partial Regex MyRegex1();
}