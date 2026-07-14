using MathEval.Exceptions;
using MathEval.Functions;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;
using InvalidOpException = MathEval.Exceptions.InvalidOperationException;

namespace MathEval.Context;

public class ExpressionContext {
    private readonly ExpressionContext? _parent;
    private readonly ConcurrentDictionary<string, SymbolEntry> _symbols;
    private readonly ConcurrentDictionary<string, FunctionEntry> _functions;

    internal readonly record struct FunctionEntry(ExpressionFunction Function, FunctionFlags Flags);

    // ARCH-8: 内置函数与常量通过静态 FrozenDictionary 共享，
    // 避免每次 new ExpressionContext() 重复注册 ~30 项到 ConcurrentDictionary
    private static readonly FrozenDictionary<string, FunctionEntry> s_builtInFunctions;
    private static readonly FrozenDictionary<string, double> s_builtInSymbols;

    static ExpressionContext() {
        var symbols = new Dictionary<string, double>(StringComparer.Ordinal);
        var functions = new Dictionary<string, FunctionEntry>(StringComparer.Ordinal);
        BuiltInFunctions.Populate(symbols, functions);
        s_builtInSymbols = symbols.ToFrozenDictionary(StringComparer.Ordinal);
        s_builtInFunctions = functions.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public ExpressionContext() {
        _parent = null;
        _symbols = new ConcurrentDictionary<string, SymbolEntry>(StringComparer.Ordinal);
        _functions = new ConcurrentDictionary<string, FunctionEntry>(StringComparer.Ordinal);
    }

    private ExpressionContext(ExpressionContext parent) {
        _parent = parent;
        _symbols = new ConcurrentDictionary<string, SymbolEntry>(StringComparer.Ordinal);
        _functions = new ConcurrentDictionary<string, FunctionEntry>(StringComparer.Ordinal);
    }

    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.Ordinal)
    {
        "true", "false", "and", "or", "not", "xor", "mod", "NaN", "INF"
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
    /// <param name="name">函数名</param>
    /// <param name="func">函数委托</param>
    /// <param name="flags">函数行为标记，默认为 ElementWise（逐元素操作）</param>
    public void SetFunction(string name, ExpressionFunction func, FunctionFlags flags = FunctionFlags.ElementWise) {
        if (ReservedKeywords.Contains(name)) throw new InvalidOpException($"无法使用保留关键字注册函数：{name}");

        _functions[name] = new FunctionEntry(func, flags);
    }

    /// <summary>
    /// 通过 Delegate 注册函数
    /// </summary>
    /// <param name="name">函数名</param>
    /// <param name="func">函数委托</param>
    /// <param name="flags">函数行为标记，默认为 ElementWise（逐元素操作）</param>
    public void SetFunction(string name, Delegate func, FunctionFlags flags = FunctionFlags.ElementWise) {
        if (ReservedKeywords.Contains(name)) throw new InvalidOpException($"无法使用保留关键字注册函数：{name}");

        var method = func.Method;
        var parameters = method.GetParameters();
        var argCount = parameters.Length;

        SetFunction(name, args => {
            if (args.Length != argCount) throw new FunctionTypeMismatchException($"函数 {name} 需要 {argCount} 个参数，但提供了 {args.Length} 个");

            try {
                var convertedArgs = new object?[argCount];
                for (int i = 0; i < argCount; i++) {
                    try {
                        convertedArgs[i] = Convert.ChangeType(args[i], parameters[i].ParameterType);
                    } catch (Exception ex) when (ex is not MathEvalException) {
                        // Convert.ChangeType 可抛 FormatException/OverflowException/InvalidCastException
                        throw new FunctionTypeMismatchException($"函数 {name} 第 {i + 1} 个参数类型不匹配：{ex.Message}");
                    }
                }

                try {
                    var result = method.Invoke(func.Target, convertedArgs);
                    return result!;
                } catch (TargetInvocationException ex) {
                    // 解包用户函数体内抛出的异常，重新包装为 MathEval 异常以保留异常契约
                    var inner = ex.InnerException ?? ex;
                    throw new EvaluateException($"调用函数 {name} 时出错：{inner.Message}", inner);
                } catch (Exception ex) when (ex is not MathEvalException) {
                    throw new EvaluateException($"调用函数 {name} 时出错：{ex.Message}", ex);
                }
            } catch (MathEvalException) {
                // 已为 MathEval 异常，直接透传，避免重复包装
                throw;
            }
        }, flags);
    }

    public void SetFunction<T1, TResult>(string name, Func<T1, TResult> func) {
        SetFunction(name, Internal.FunctionWrapper.Wrap(name, func));
    }

    public void SetFunction<T1, T2, TResult>(string name, Func<T1, T2, TResult> func) {
        SetFunction(name, Internal.FunctionWrapper.Wrap(name, func));
    }

    public void SetFunction<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> func) {
        SetFunction(name, Internal.FunctionWrapper.Wrap(name, func));
    }

    public void SetFunction<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> func) {
        SetFunction(name, Internal.FunctionWrapper.Wrap(name, func));
    }

    public void SetFunction<T1, T2, T3, T4, T5, TResult>(string name, Func<T1, T2, T3, T4, T5, TResult> func) {
        SetFunction(name, Internal.FunctionWrapper.Wrap(name, func));
    }

    public void SetFunction<T1, T2, T3, T4, T5, T6, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, TResult> func) {
        SetFunction(name, Internal.FunctionWrapper.Wrap(name, func));
    }

    public void SetFunction<T1, T2, T3, T4, T5, T6, T7, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, TResult> func) {
        SetFunction(name, Internal.FunctionWrapper.Wrap(name, func));
    }

    public void SetFunction<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> func) {
        SetFunction(name, Internal.FunctionWrapper.Wrap(name, func));
    }

    public bool TryGetSymbol(string name, out object value) {
        if (_symbols.TryGetValue(name, out var entry)) {
            value = entry.GetValue();
            return true;
        }

        if (_parent != null) return _parent.TryGetSymbol(name, out value);

        // 回退到静态内置常量表（E、PI、π）
        if (s_builtInSymbols.TryGetValue(name, out var constValue)) {
            value = constValue;
            return true;
        }

        value = null!;
        return false;
    }

    /// <summary>
    /// 在当前上下文链（含父级）中解析函数条目，根级回退到静态内置函数表
    /// </summary>
    internal bool TryGetFunctionEntry(string name, out FunctionEntry entry) {
        if (_functions.TryGetValue(name, out entry)) return true;
        if (_parent != null) return _parent.TryGetFunctionEntry(name, out entry);
        return s_builtInFunctions.TryGetValue(name, out entry);
    }

    public bool TryGetFunction(string name, out ExpressionFunction func) {
        if (TryGetFunctionEntry(name, out var entry)) {
            func = entry.Function;
            return true;
        }

        func = null!;
        return false;
    }

    /// <summary>
    /// 判断指定函数是否为聚合函数
    /// </summary>
    internal bool IsAggregateFunction(string name) {
        return TryGetFunctionEntry(name, out var entry) && entry.Flags.HasFlag(FunctionFlags.Aggregate);
    }

    /// <summary>
    /// 获取当前上下文（含父级）中所有聚合函数名集合（含内置聚合函数 max/min）
    /// </summary>
    internal HashSet<string> GetAggregateFunctionNames() {
        var set = new HashSet<string>(StringComparer.Ordinal);
        CollectAggregateFunctions(set);
        // 包含内置聚合函数，确保 IndexPushdownOptimizer 能正确跳过
        foreach (var kvp in s_builtInFunctions) {
            if (kvp.Value.Flags.HasFlag(FunctionFlags.Aggregate))
                set.Add(kvp.Key);
        }
        return set;
    }

    private void CollectAggregateFunctions(HashSet<string> set) {
        foreach (var kvp in _functions) {
            if (kvp.Value.Flags.HasFlag(FunctionFlags.Aggregate))
                set.Add(kvp.Key);
        }
        _parent?.CollectAggregateFunctions(set);
    }

    public ExpressionContext CreateChild() {
        return new ExpressionContext(this);
    }

    public void RemoveSymbol(string name) {
        _symbols.TryRemove(name, out _);
    }

    public void RemoveFunction(string name) {
        _functions.TryRemove(name, out _);
    }

    private class SymbolEntry {
        public object? DirectValue { get; init; }
        public Func<object>? LazyValue { get; init; }
        public bool IsLazy => LazyValue != null;

        public object GetValue() => IsLazy ? LazyValue!() : DirectValue!;
    }
}
