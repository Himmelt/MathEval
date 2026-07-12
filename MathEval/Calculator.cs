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
    private readonly ExpressionOptions _options = options;
    private readonly ExpressionContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly string _expressionText = expression ?? throw new ArgumentNullException(nameof(expression));

    public object Eval() {
        EnsureParsed();

        // 如果启用了编译优化，使用编译后的委托
        if (_options.HasFlag(ExpressionOptions.CompileOptimization)) {
            EnsureCompiled();
            return _compiledExpression!.Evaluate(_context);
        }

        // 否则使用原始的 Visitor 模式
        var visitor = new EvaluationVisitor(_context);
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

        // 尝试从缓存获取（缓存键包含表达式文本与选项，避免不同选项共享 AST）
        if (!_options.HasFlag(ExpressionOptions.NoCache) && ExpressionCache.TryGet(_expressionText, (int)_options, out var cachedAst)) {
            _ast = cachedAst;
        } else {
            var lexer = new Lexer.Lexer(_expressionText);
            var parser = new Parser.Parser(lexer);
            _ast = parser.Parse();

            // 应用常量折叠优化（受 ConstantFolding 选项控制）
            if (_options.HasFlag(ExpressionOptions.ConstantFolding)) {
                _ast = ConstantFolder.Fold(_ast);
            }

            // 应用索引下推优化（默认开启，可用 DisableIndexPushdown 关闭）
            if (!_options.HasFlag(ExpressionOptions.DisableIndexPushdown)) {
                _ast = IndexPushdownOptimizer.Optimize(_ast);
            }

            if (!_options.HasFlag(ExpressionOptions.NoCache)) ExpressionCache.Set(_expressionText, (int)_options, _ast);
        }
    }

    private void EnsureCompiled() {
        if (_compiledExpression != null) return;
        EnsureParsed();

        // 尝试从优化缓存获取编译后的表达式
        if (!_options.HasFlag(ExpressionOptions.NoCache) &&
            OptimizedExpressionCache.TryGetCompiled(_expressionText, (int)_options, out var cachedCompiled)) {
            _compiledExpression = cachedCompiled;
            return;
        }

        // 编译表达式
        _compiledExpression = new CompiledExpression(_ast!);

        // 缓存编译后的表达式
        if (!_options.HasFlag(ExpressionOptions.NoCache)) {
            OptimizedExpressionCache.SetCompiled(_expressionText, (int)_options, _compiledExpression);
        }
    }
}