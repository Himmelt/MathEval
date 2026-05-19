using System.Collections.Concurrent;
using MathEval.Functions;

namespace MathEval.Context;

public class ExpressionContext
{
    private readonly ExpressionContext? _parent;
    private readonly ConcurrentDictionary<string, SymbolEntry> _symbols;
    private readonly ConcurrentDictionary<string, ExpressionFunction> _functions;

    public ExpressionContext()
    {
        _parent = null;
        _symbols = new ConcurrentDictionary<string, SymbolEntry>(StringComparer.Ordinal);
        _functions = new ConcurrentDictionary<string, ExpressionFunction>(StringComparer.Ordinal);
        BuiltInFunctions.Register(this);
    }

    private ExpressionContext(ExpressionContext parent)
    {
        _parent = parent;
        _symbols = new ConcurrentDictionary<string, SymbolEntry>(StringComparer.Ordinal);
        _functions = new ConcurrentDictionary<string, ExpressionFunction>(StringComparer.Ordinal);
    }

    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "false", "and", "or", "not", "xor", "NaN", "INF"
    };

    public void Set(string name, object value)
    {
        if (ReservedKeywords.Contains(name))
            throw new InvalidOperationException($"Cannot set reserved keyword: {name}");

        if (_functions.ContainsKey(name))
            throw new InvalidOperationException($"A function with name '{name}' already exists");

        _symbols[name] = new SymbolEntry { DirectValue = value };
    }

    public void Set(string name, Func<object> value)
    {
        if (ReservedKeywords.Contains(name))
            throw new InvalidOperationException($"Cannot set reserved keyword: {name}");

        if (_functions.ContainsKey(name))
            throw new InvalidOperationException($"A function with name '{name}' already exists");

        _symbols[name] = new SymbolEntry { LazyValue = value };
    }

    public void SetFunction(string name, ExpressionFunction func)
    {
        if (ReservedKeywords.Contains(name))
            throw new InvalidOperationException($"Cannot register function with reserved keyword: {name}");

        _symbols.TryRemove(name, out _);
        _functions[name] = func;
    }

    public void SetFunction<T1, TResult>(string name, Func<T1, TResult> func)
    {
        SetFunction(name, Internal.FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, TResult>(string name, Func<T1, T2, TResult> func)
    {
        SetFunction(name, Internal.FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> func)
    {
        SetFunction(name, Internal.FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> func)
    {
        SetFunction(name, Internal.FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, T4, T5, TResult>(string name, Func<T1, T2, T3, T4, T5, TResult> func)
    {
        SetFunction(name, Internal.FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, T4, T5, T6, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, TResult> func)
    {
        SetFunction(name, Internal.FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, T4, T5, T6, T7, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, TResult> func)
    {
        SetFunction(name, Internal.FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> func)
    {
        SetFunction(name, Internal.FunctionWrapper.Wrap(func));
    }

    public bool TryGetSymbol(string name, out object value)
    {
        if (_symbols.TryGetValue(name, out var entry))
        {
            value = entry.GetValue();
            return true;
        }

        if (_parent != null)
            return _parent.TryGetSymbol(name, out value);

        value = null!;
        return false;
    }

    public bool TryGetFunction(string name, out ExpressionFunction func)
    {
        if (_functions.TryGetValue(name, out func))
            return true;

        if (_parent != null)
            return _parent.TryGetFunction(name, out func);

        func = null!;
        return false;
    }

    public ExpressionContext CreateChild()
    {
        return new ExpressionContext(this);
    }

    public void Remove(string name)
    {
        _symbols.TryRemove(name, out _);
        _functions.TryRemove(name, out _);
    }

    private class SymbolEntry
    {
        public object? DirectValue { get; init; }
        public Func<object>? LazyValue { get; init; }
        public bool IsLazy => LazyValue != null;

        public object GetValue() => IsLazy ? LazyValue!() : DirectValue!;
    }
}
