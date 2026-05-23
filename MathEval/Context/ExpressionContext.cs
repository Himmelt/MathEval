using MathEval.Exceptions;
using MathEval.Functions;
using System.Collections.Concurrent;
using System.Reflection;
using InvalidOpException = MathEval.Exceptions.InvalidOperationException;

namespace MathEval.Context;

public class ExpressionContext {
    private readonly ExpressionContext? _parent;
    private readonly ConcurrentDictionary<string, SymbolEntry> _symbols;
    private readonly ConcurrentDictionary<string, ExpressionFunction> _functions;

    public ExpressionContext() {
        _parent = null;
        _symbols = new ConcurrentDictionary<string, SymbolEntry>(StringComparer.Ordinal);
        _functions = new ConcurrentDictionary<string, ExpressionFunction>(StringComparer.Ordinal);
        BuiltInFunctions.Register(this);
    }

    private ExpressionContext(ExpressionContext parent) {
        _parent = parent;
        _symbols = new ConcurrentDictionary<string, SymbolEntry>(StringComparer.Ordinal);
        _functions = new ConcurrentDictionary<string, ExpressionFunction>(StringComparer.Ordinal);
    }

    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "false", "and", "or", "not", "xor", "NaN", "INF"
    };

    /// <summary>
    /// 注册直接值符号
    /// </summary>
    public void Set(string name, object value) {
        if (ReservedKeywords.Contains(name)) throw new InvalidOpException($"无法使用保留关键字注册符号：{name}");

        _symbols[name] = new SymbolEntry { DirectValue = value };
    }

    /// <summary>
    /// 注册延迟值符号
    /// 注意：对于延迟值，由用户保证其 线程安全 和 异常处理！！！
    /// </summary>
    public void Set(string name, Func<object> value) {
        if (ReservedKeywords.Contains(name)) throw new InvalidOpException($"无法使用保留关键字注册符号：{name}");

        _symbols[name] = new SymbolEntry { LazyValue = value };
    }

    /// <summary>
    /// 注册自定义函数
    /// </summary>
    public void SetFunction(string name, ExpressionFunction func) {
        if (ReservedKeywords.Contains(name)) throw new InvalidOpException($"无法使用保留关键字注册函数：{name}");

        _functions[name] = func;
    }

    /// <summary>
    /// 通过 Delegate 注册函数
    /// </summary>
    public void SetFunction(string name, Delegate func) {
        if (ReservedKeywords.Contains(name)) throw new InvalidOpException($"无法使用保留关键字注册函数：{name}");

        var method = func.Method;
        var parameters = method.GetParameters();
        var argCount = parameters.Length;

        SetFunction(name, args => {
            if (args.Length != argCount) throw new FunctionTypeMismatchException($"函数 '{name}' 需要 {argCount} 个参数，但提供了 {args.Length} 个");

            try {
                var convertedArgs = new object?[argCount];
                for (int i = 0; i < argCount; i++) {
                    convertedArgs[i] = Convert.ChangeType(args[i], parameters[i].ParameterType);
                }
                var result = method.Invoke(func.Target, convertedArgs);
                return result!;
            } catch (InvalidCastException) {
                throw new FunctionTypeMismatchException($"函数 '{name}' 的参数类型不匹配");
            } catch (TargetInvocationException ex) {
                throw new FunctionTypeMismatchException($"调用函数 '{name}' 时出错：{ex.InnerException?.Message}");
            }
        });
    }

    public void SetFunction<T1, TResult>(string name, Func<T1, TResult> func) {
        SetFunction(name, Internal.FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, TResult>(string name, Func<T1, T2, TResult> func) {
        SetFunction(name, Internal.FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> func) {
        SetFunction(name, Internal.FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> func) {
        SetFunction(name, Internal.FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, T4, T5, TResult>(string name, Func<T1, T2, T3, T4, T5, TResult> func) {
        SetFunction(name, Internal.FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, T4, T5, T6, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, TResult> func) {
        SetFunction(name, Internal.FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, T4, T5, T6, T7, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, TResult> func) {
        SetFunction(name, Internal.FunctionWrapper.Wrap(func));
    }

    public void SetFunction<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> func) {
        SetFunction(name, Internal.FunctionWrapper.Wrap(func));
    }

    public bool TryGetSymbol(string name, out object value) {
        if (_symbols.TryGetValue(name, out var entry)) {
            value = entry.GetValue();
            return true;
        }

        if (_parent != null) return _parent.TryGetSymbol(name, out value);

        value = null!;
        return false;
    }

    public bool TryGetFunction(string name, out ExpressionFunction func) {
        if (_functions.TryGetValue(name, out func!)) return true;

        if (_parent != null) return _parent.TryGetFunction(name, out func);

        func = null!;
        return false;
    }

    public ExpressionContext CreateChild() {
        return new ExpressionContext(this);
    }

    public void Remove(string name) {
        _symbols.TryRemove(name, out _);
        _functions.TryRemove(name, out _);
    }

    private class SymbolEntry {
        public object? DirectValue { get; init; }
        public Func<object>? LazyValue { get; init; }
        public bool IsLazy => LazyValue != null;

        public object GetValue() => IsLazy ? LazyValue!() : DirectValue!;
    }
}