using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using MathEval.Context;
using MathEval.Fast;

BenchmarkRunner.Run<ComparisonBenchmarks>(args: args);

[MemoryDiagnoser]
[RankColumn]
public class ComparisonBenchmarks {
    // 测试表达式
    private const string SimpleArithmetic = "1 + 2 * 3 - 4 / 2";
    private const string ComplexArithmetic = "(3.14 + 2.72) * (1.41 - 0.58) / (3.67 + 1.23) + 2 ^ 3";
    private const string FunctionCall = "sin(0.5) + cos(0.3) * sqrt(16) - abs(-5)";
    private const string NestedFunction = "sqrt(pow(sin(0.5), 2) + pow(cos(0.5), 2))";
    private const string Conditional = "1 > 0 ? 42 : -1";
    private const string Logical = "1 > 0 and 2 < 3 ? 100 : 0";
    private const string LogExpression = "log(100) + ln(2.718) + log(8, 2)";

    // MathEval 上下文
    private ExpressionContext? _mathEvalContext;

    // 用于生成唯一表达式的计数器
    private int _counter;

    [GlobalSetup]
    public void Setup() {
        _mathEvalContext = new ExpressionContext();
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

    [Benchmark(Description = "NCalc: 简单算术")]
    [BenchmarkCategory("SimpleArithmetic")]
    public object? NCalc_SimpleArithmetic() {
        var expr = new NCalc.Expression(MakeUnique(SimpleArithmetic), NCalc.EvaluateOptions.NoCache);
        return expr.Evaluate();
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

    [Benchmark(Description = "NCalc: 复杂算术")]
    [BenchmarkCategory("ComplexArithmetic")]
    public object? NCalc_ComplexArithmetic() {
        var expr = new NCalc.Expression(MakeUnique(ComplexArithmetic), NCalc.EvaluateOptions.NoCache);
        return expr.Evaluate();
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

    [Benchmark(Description = "NCalc: 函数调用")]
    [BenchmarkCategory("FunctionCall")]
    public object? NCalc_FunctionCall() {
        var expr = new NCalc.Expression(MakeUnique(FunctionCall), NCalc.EvaluateOptions.NoCache);
        expr.EvaluateFunction += (name, args) => {
            if (name == "sin") args.Result = Math.Sin(Convert.ToDouble(args.Parameters[0].Evaluate()));
            else if (name == "cos") args.Result = Math.Cos(Convert.ToDouble(args.Parameters[0].Evaluate()));
            else if (name == "sqrt") args.Result = Math.Sqrt(Convert.ToDouble(args.Parameters[0].Evaluate()));
            else if (name == "abs") args.Result = Math.Abs(Convert.ToDouble(args.Parameters[0].Evaluate()));
        };
        return expr.Evaluate();
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

    [Benchmark(Description = "NCalc: 嵌套函数")]
    [BenchmarkCategory("NestedFunction")]
    public object? NCalc_NestedFunction() {
        var expr = new NCalc.Expression(MakeUnique(NestedFunction), NCalc.EvaluateOptions.NoCache);
        expr.EvaluateFunction += (name, args) => {
            if (name == "sqrt") args.Result = Math.Sqrt(Convert.ToDouble(args.Parameters[0].Evaluate()));
            else if (name == "pow") args.Result = Math.Pow(Convert.ToDouble(args.Parameters[0].Evaluate()), Convert.ToDouble(args.Parameters[1].Evaluate()));
            else if (name == "sin") args.Result = Math.Sin(Convert.ToDouble(args.Parameters[0].Evaluate()));
            else if (name == "cos") args.Result = Math.Cos(Convert.ToDouble(args.Parameters[0].Evaluate()));
        };
        return expr.Evaluate();
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

    [Benchmark(Description = "NCalc: 三元运算")]
    [BenchmarkCategory("Conditional")]
    public object? NCalc_Conditional() {
        var expr = new NCalc.Expression(MakeUnique(Conditional), NCalc.EvaluateOptions.NoCache);
        return expr.Evaluate();
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

    [Benchmark(Description = "NCalc: 逻辑运算")]
    [BenchmarkCategory("Logical")]
    public object? NCalc_Logical() {
        var expr = new NCalc.Expression(MakeUnique(Logical), NCalc.EvaluateOptions.NoCache);
        return expr.Evaluate();
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

    [Benchmark(Description = "NCalc: 对数")]
    [BenchmarkCategory("Log")]
    public object? NCalc_Log() {
        var expr = new NCalc.Expression(MakeUnique(LogExpression), NCalc.EvaluateOptions.NoCache);
        expr.EvaluateFunction += (name, args) => {
            if (name == "log") {
                if (args.Parameters.Length == 1)
                    args.Result = Math.Log10(Convert.ToDouble(args.Parameters[0].Evaluate()));
                else
                    args.Result = Math.Log(Convert.ToDouble(args.Parameters[0].Evaluate()), Convert.ToDouble(args.Parameters[1].Evaluate()));
            }
            else if (name == "ln") {
                args.Result = Math.Log(Convert.ToDouble(args.Parameters[0].Evaluate()));
            }
        };
        return expr.Evaluate();
    }
}