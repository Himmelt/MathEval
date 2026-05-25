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
        var targetType = typeof(T);

        if (result is T typedResult) return typedResult;

        if (result is double d) {
            if (targetType == typeof(double)) return (T)(object)d;
            if (targetType == typeof(float)) return (T)(object)(float)d;
            if (targetType == typeof(decimal)) return (T)(object)(decimal)d;

            if (IsIntegerType(targetType)) {
                if (!IsMathematicalInteger(d)) {
                    throw new TypeMismatchException(
                        $"无法将非整数 {d} 转换为整数类型 {targetType.Name}",
                        targetType.Name, "double");
                }

                try {
                    return (T)Convert.ChangeType(d, targetType);
                } catch (System.OverflowException) {
                    throw new Exceptions.OverflowException(
                        $"值 {d} 超出 {targetType.Name} 类型的范围");
                }
            }
        }

        if (result is bool b && targetType == typeof(bool)) return (T)(object)b;
        if (result is string s && targetType == typeof(string)) return (T)(object)s;

        try {
            return (T)Convert.ChangeType(result, targetType);
        } catch (InvalidCastException) {
            throw new TypeMismatchException(
                $"无法将结果转换为类型 {targetType.Name}",
                targetType.Name, result?.GetType().Name ?? "null");
        } catch (System.OverflowException) {
            throw new Exceptions.OverflowException(
                $"值超出 {targetType.Name} 类型的范围");
        }
    }

    private static bool IsIntegerType(Type type) {
        return type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong);
    }

    private static bool IsMathematicalInteger(double value) {
        if (double.IsNaN(value) || double.IsInfinity(value)) return false;
        return value == Math.Truncate(value);
    }

    public void Set(string name, object value) => _context.Set(name, value);

    public void Set(string name, Func<object> value) => _context.Set(name, value);

    public void Remove(string name) => _context.Remove(name);

    private void EnsureParsed() {
        if (_ast != null) return;

        if (string.IsNullOrWhiteSpace(_expressionText)) throw new ParseException("表达式不能为空或仅包含空白字符", 1, 1);

        // 尝试从缓存获取
        if (!_options.HasFlag(ExpressionOptions.NoCache) && ExpressionCache.TryGet(_expressionText, out var cachedAst)) {
            _ast = cachedAst;
        } else {
            var lexer = new Lexer.Lexer(_expressionText);
            var parser = new Parser.Parser(lexer);
            _ast = parser.Parse();

            // 应用常量折叠优化
            if (_options.HasFlag(ExpressionOptions.ConstantFolding)) {
                _ast = ConstantFolder.Fold(_ast);
            }

            if (!_options.HasFlag(ExpressionOptions.NoCache)) ExpressionCache.Set(_expressionText, _ast);
        }
    }

    private void EnsureCompiled() {
        if (_compiledExpression != null) return;
        EnsureParsed();

        // 尝试从优化缓存获取编译后的表达式
        if (!_options.HasFlag(ExpressionOptions.NoCache) &&
            OptimizedExpressionCache.TryGetCompiled(_expressionText, out var cachedCompiled)) {
            _compiledExpression = cachedCompiled;
            return;
        }

        // 编译表达式
        _compiledExpression = new CompiledExpression(_ast!);

        // 缓存编译后的表达式
        if (!_options.HasFlag(ExpressionOptions.NoCache)) {
            OptimizedExpressionCache.SetCompiled(_expressionText, _compiledExpression);
        }
    }
}