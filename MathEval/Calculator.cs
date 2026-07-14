using MathEval.AST;
using MathEval.Context;
using MathEval.Exceptions;
using MathEval.Internal;
using MathEval.Optimization;
using MathEval.Visitors;

namespace MathEval;

public class Calculator(string expression, ExpressionContext context, ExpressionOptions options = ExpressionOptions.None) : ICalculator {

    private LogicalExpression? _ast;
    private CompiledExpression? _compiledExpression;
    private EvaluationVisitor? _visitor;
    private readonly ExpressionOptions _options = options;
    private readonly ExpressionContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly string _expressionText = expression ?? throw new ArgumentNullException(nameof(expression));

    /// <summary>
    /// 最大嵌套深度（控制表达式嵌套层次上限，防止栈溢出）
    /// 默认 <see cref="Parser.Parser.DefaultMaxDepth"/> = 1024
    /// </summary>
    public int MaxNestingDepth { get; set; } = Parser.Parser.DefaultMaxDepth;

    public object Eval() {
        EnsureParsed();

        // 如果启用了编译优化，使用编译后的委托
        if (_options.HasFlag(ExpressionOptions.CompileOptimization)) {
            EnsureCompiled();
            return _compiledExpression!.Evaluate(_context);
        }

        // 否则使用原始的 Visitor 模式（复用 visitor 实例减少 GC 压力 OPT-2）
        var visitor = _visitor ??= new EvaluationVisitor(_context);
        return _ast!.Accept(visitor);
    }

    public T Eval<T>() {
        var result = Eval();
        return ConvertResult<T>(result);
    }

    private static T ConvertResult<T>(object result) {
        if (result is T typedResult) return typedResult;

        var targetType = typeof(T);

        if (result is double d) {
            if (targetType == typeof(double)) return (T)(object)d;
            if (targetType == typeof(float)) return (T)(object)(float)d;
            if (targetType == typeof(bool)) return (T)(object)(d != 0);
            if (targetType == typeof(string)) return (T)(object)d.ToString();
            if (targetType == typeof(int)) return (T)(object)(int)d;
            if (targetType == typeof(long)) return (T)(object)(long)d;
            if (targetType == typeof(decimal)) return (T)(object)(decimal)d;
            if (targetType == typeof(sbyte)) return (T)(object)(sbyte)d;
            if (targetType == typeof(byte)) return (T)(object)(byte)d;
            if (targetType == typeof(short)) return (T)(object)(short)d;
            if (targetType == typeof(ushort)) return (T)(object)(ushort)d;
            if (targetType == typeof(uint)) return (T)(object)(uint)d;
            if (targetType == typeof(ulong)) return (T)(object)(ulong)d;
        }

        if (result is double[] arr) {
            if (targetType == typeof(double[])) return (T)(object)arr;
            if (targetType == typeof(List<double>)) return (T)(object)arr.ToList();
        }

        // Handle non-double numeric types from context variables
        if (result is IConvertible conv) {
            try {
                return (T)Convert.ChangeType(conv, targetType);
            } catch (InvalidCastException) { } catch (FormatException) { } catch (System.OverflowException) { }
        }

        throw new TypeMismatchException(
            $"无法将 {result?.GetType().Name ?? "null"} 转换为 {typeof(T).Name}",
            typeof(T).Name, result?.GetType().Name ?? "null");
    }

    public void Set(string name, object value) => _context.Set(name, value);

    public void Set(string name, Func<object> value) => _context.Set(name, value);

    public void RemoveSymbol(string name) => _context.RemoveSymbol(name);

    public void RemoveFunction(string name) => _context.RemoveFunction(name);

    private void EnsureParsed() {
        if (_ast != null) return;

        if (string.IsNullOrWhiteSpace(_expressionText)) throw new ParseException("表达式不能为空或仅包含空白字符", 1, 1);

        // OPT-7: 使用 GetOrAdd 代替 TryGet + Set，避免并发首跑时重复解析同一表达式
        if (_options.HasFlag(ExpressionOptions.NoCache)) {
            _ast = ParseAndOptimize();
        } else {
            _ast = OptimizedExpressionCache.GetOrAdd(_expressionText, _ => ParseAndOptimize());
        }
    }

    /// <summary>
    /// 解析表达式并应用优化（索引下推 + 常量折叠）
    /// </summary>
    private LogicalExpression ParseAndOptimize() {
        var lexer = new Lexer.Lexer(_expressionText);
        var parser = new Parser.Parser(lexer, MaxNestingDepth);
        var ast = parser.Parse();

        // 应用索引下推优化（仅对非聚合函数下推索引，避免改变聚合函数语义）
        ast = IndexPushdownOptimizer.Optimize(ast, _context.GetAggregateFunctionNames());

        // 应用常量折叠优化（在索引下推之后运行，可折叠下推产生的模式，如 ([1,2,3]*2)[0] → 1*2 → 2）
        if (_options.HasFlag(ExpressionOptions.ConstantFolding)) {
            ast = ConstantFolder.Fold(ast);
        }

        return ast;
    }

    private void EnsureCompiled() {
        if (_compiledExpression != null) return;
        EnsureParsed();

        // OPT-8: 使用 GetOrAddCompiled 代替 TryGetCompiled + SetCompiled，
        // 内部双重检查锁定避免并发首跑重复编译
        if (_options.HasFlag(ExpressionOptions.NoCache)) {
            _compiledExpression = new CompiledExpression(_ast!);
        } else {
            _compiledExpression = OptimizedExpressionCache.GetOrAddCompiled(
                _expressionText,
                _ => _ast!,
                ast => new CompiledExpression(ast)
            );
        }
    }
}