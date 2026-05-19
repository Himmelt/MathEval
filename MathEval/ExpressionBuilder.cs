using MathEval.Context;

namespace MathEval;

/// <summary>
/// 表达式构建器，使用流畅的 API 模式配置上下文
/// </summary>
public class ExpressionBuilder
{
    private readonly ExpressionContext _context = new();
    private ExpressionOptions _options = ExpressionOptions.None;

    public ExpressionBuilder With(string name, object value)
    {
        _context.Set(name, value);
        return this;
    }

    public ExpressionBuilder With(string name, Func<object> value)
    {
        _context.Set(name, value);
        return this;
    }

    public ExpressionBuilder WithFunction(string name, ExpressionFunction func)
    {
        _context.SetFunction(name, func);
        return this;
    }

    public ExpressionBuilder WithFunction(string name, Delegate func)
    {
        _context.SetFunction(name, func);
        return this;
    }

    public ExpressionBuilder WithFunction<T1, TResult>(string name, Func<T1, TResult> func)
    {
        _context.SetFunction(name, func);
        return this;
    }

    public ExpressionBuilder WithFunction<T1, T2, TResult>(string name, Func<T1, T2, TResult> func)
    {
        _context.SetFunction(name, func);
        return this;
    }

    public ExpressionBuilder WithFunction<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> func)
    {
        _context.SetFunction(name, func);
        return this;
    }

    public ExpressionBuilder WithFunction<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> func)
    {
        _context.SetFunction(name, func);
        return this;
    }

    public ExpressionBuilder WithFunction<T1, T2, T3, T4, T5, TResult>(string name, Func<T1, T2, T3, T4, T5, TResult> func)
    {
        _context.SetFunction(name, func);
        return this;
    }

    public ExpressionBuilder WithFunction<T1, T2, T3, T4, T5, T6, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, TResult> func)
    {
        _context.SetFunction(name, func);
        return this;
    }

    public ExpressionBuilder WithFunction<T1, T2, T3, T4, T5, T6, T7, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, TResult> func)
    {
        _context.SetFunction(name, func);
        return this;
    }

    public ExpressionBuilder WithFunction<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> func)
    {
        _context.SetFunction(name, func);
        return this;
    }

    public ExpressionBuilder WithOptions(ExpressionOptions options)
    {
        _options = options;
        return this;
    }

    public ICalculator Build(string expression)
    {
        return new Calculator(expression, _context, _options);
    }
}
