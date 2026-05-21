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
        if (result is T typedResult)
            return typedResult;
        try {
            return (T)Convert.ChangeType(result, typeof(T));
        } catch (InvalidCastException) {
            throw new TypeMismatchException($"无法将结果转换为类型 {typeof(T).Name}", typeof(T).Name, result?.GetType().Name ?? "null");
        }
    }

    public void Set(string name, object value) => _context.Set(name, value);

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