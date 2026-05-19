using MathEval.AST;
using MathEval.Context;
using MathEval.Exceptions;
using MathEval.Internal;
using MathEval.Visitors;

namespace MathEval;

public class Calculator(string expression, ExpressionContext context, ExpressionOptions options = ExpressionOptions.None) : ICalculator {

    private LogicalExpression? _ast;
    private readonly ExpressionOptions _options = options;
    private readonly ExpressionContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly string _expressionText = expression ?? throw new ArgumentNullException(nameof(expression));

    public object Eval() {
        EnsureParsed();
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

    public void Set(string name, Func<object> value) => _context.Set(name, value);

    public void Remove(string name) => _context.Remove(name);

    private void EnsureParsed() {
        if (_ast != null) return;

        if (string.IsNullOrWhiteSpace(_expressionText)) throw new ParseException("表达式不能为空或仅包含空白字符", 1, 1);

        if (!_options.HasFlag(ExpressionOptions.NoCache) && ExpressionCache.TryGet(_expressionText, out _ast)) return;

        var lexer = new Lexer.Lexer(_expressionText);
        var parser = new Parser.Parser(lexer);
        _ast = parser.Parse();

        if (!_options.HasFlag(ExpressionOptions.NoCache)) ExpressionCache.Set(_expressionText, _ast);
    }
}