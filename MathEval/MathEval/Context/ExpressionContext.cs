using MathEval.Exceptions;
using MathEval.TypeSystem;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;
using InvalidOpException = MathEval.Exceptions.InvalidOperationException;

namespace MathEval.Context;

public class ExpressionContext {
    private readonly ExpressionContext? _parent;
    private readonly ConcurrentDictionary<string, SymbolEntry> _symbols;
    private readonly ConcurrentDictionary<string, FunctionEntry> _functions;

    private long _symbolVersion;
    private static long s_nextContextId;

    /// <summary>Kind 推断缓存键用的上下文实例标识（程序集内唯一）</summary>
    internal long ContextId { get; }

    /// <summary>
    /// 符号/函数集合的版本号：每次 Set/Remove（符号或函数）递增，
    /// 父上下文的变更经链路实时汇总。KindInferenceCache 以此判定推断结果是否失效
    /// </summary>
    public long SymbolVersion => _symbolVersion + (_parent?.SymbolVersion ?? 0);

    internal readonly record struct FunctionEntry(ExpressionFunction Function, FunctionFlags Flags,
        MathKind?[]? ParamKinds = null, MathKind? ResultKind = null);

    // ARCH-8: 内置函数与常量通过静态 FrozenDictionary 共享，
    // 避免每次 new ExpressionContext() 重复注册 ~30 项到 ConcurrentDictionary
    private static readonly FrozenDictionary<string, FunctionEntry> s_builtInFunctions;
    private static readonly FrozenDictionary<string, double> s_builtInSymbols;

    static ExpressionContext() {
        var symbols = new Dictionary<string, double>(StringComparer.Ordinal);
        var functions = new Dictionary<string, FunctionEntry>(StringComparer.Ordinal);
        BuiltInEntries.Populate(symbols, functions);
        s_builtInSymbols = symbols.ToFrozenDictionary(StringComparer.Ordinal);
        s_builtInFunctions = functions.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public ExpressionContext() {
        _parent = null;
        _symbols = new ConcurrentDictionary<string, SymbolEntry>(StringComparer.Ordinal);
        _functions = new ConcurrentDictionary<string, FunctionEntry>(StringComparer.Ordinal);
        ContextId = Interlocked.Increment(ref s_nextContextId);
    }

    private ExpressionContext(ExpressionContext parent) {
        _parent = parent;
        _symbols = new ConcurrentDictionary<string, SymbolEntry>(StringComparer.Ordinal);
        _functions = new ConcurrentDictionary<string, FunctionEntry>(StringComparer.Ordinal);
        ContextId = Interlocked.Increment(ref s_nextContextId);
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
        _symbolVersion++;
    }

    /// <summary>
    /// 注册延迟值符号
    /// 注意：对于延迟值，由用户保证其 线程安全 和 异常处理！！！
    /// </summary>
    public void Set(string name, Func<object> value) {
        if (ReservedKeywords.Contains(name)) throw new InvalidOpException($"无法使用保留关键字注册符号：{name}");

        _symbols[name] = new SymbolEntry { LazyValue = value };
        _symbolVersion++;
    }

    /// <summary>
    /// 注册自定义函数
    /// </summary>
    /// <param name="name">函数名</param>
    /// <param name="func">函数委托</param>
    /// <param name="flags">函数行为标记，默认为 ElementWise（逐元素操作）</param>
    /// <param name="paramKinds">参数 Kind 签名（可空：null 表示该参数不约束），供 StrictTypes 静态检查</param>
    /// <param name="resultKind">返回值 Kind 签名（可空），供 StrictTypes 静态检查</param>
    public void SetFunction(string name, ExpressionFunction func, FunctionFlags flags = FunctionFlags.ElementWise,
        MathKind?[]? paramKinds = null, MathKind? resultKind = null) {
        if (ReservedKeywords.Contains(name)) throw new InvalidOpException($"无法使用保留关键字注册函数：{name}");

        _functions[name] = new FunctionEntry(func, flags, paramKinds, resultKind);
        _symbolVersion++;
    }

    /// <summary>
    /// 通过 Delegate 注册函数：按方法签名捕获参数与返回值的 Kind，供 StrictTypes 推断使用
    /// </summary>
    /// <param name="name">函数名</param>
    /// <param name="func">函数委托</param>
    /// <param name="flags">函数行为标记，默认为 ElementWise（逐元素操作）</param>
    public void SetFunction(string name, Delegate func, FunctionFlags flags = FunctionFlags.ElementWise) {
        if (ReservedKeywords.Contains(name)) throw new InvalidOpException($"无法使用保留关键字注册函数：{name}");

        var method = func.Method;
        var parameters = method.GetParameters();
        var argCount = parameters.Length;

        var paramKinds = new MathKind?[argCount];
        for (int i = 0; i < argCount; i++)
            paramKinds[i] = KindOfParameter(parameters[i].ParameterType);
        var resultKind = method.ReturnType == typeof(void) ? null : KindOfParameter(method.ReturnType);

        // 捕获的 Kind 签名随条目保存，供 KindInferencePass 做静态参数/返回类型检查
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
        }, flags, paramKinds, resultKind);
    }

    // 强类型重载：Wrap 提供编译期类型安全的调用委托（ARCH-10），
    // 同时按泛型参数捕获 Kind 签名，供 StrictTypes 静态检查（如 greet(1) 求值前报出）
    public void SetFunction<T1, TResult>(string name, Func<T1, TResult> func) {
        SetFunction(name, FunctionWrapper.Wrap(name, func),
            paramKinds: [KindOfParameter(typeof(T1))], resultKind: KindOfParameter(typeof(TResult)));
    }

    public void SetFunction<T1, T2, TResult>(string name, Func<T1, T2, TResult> func) {
        SetFunction(name, FunctionWrapper.Wrap(name, func),
            paramKinds: [KindOfParameter(typeof(T1)), KindOfParameter(typeof(T2))],
            resultKind: KindOfParameter(typeof(TResult)));
    }

    public void SetFunction<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> func) {
        SetFunction(name, FunctionWrapper.Wrap(name, func),
            paramKinds: [KindOfParameter(typeof(T1)), KindOfParameter(typeof(T2)), KindOfParameter(typeof(T3))],
            resultKind: KindOfParameter(typeof(TResult)));
    }

    public void SetFunction<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> func) {
        SetFunction(name, FunctionWrapper.Wrap(name, func),
            paramKinds: [KindOfParameter(typeof(T1)), KindOfParameter(typeof(T2)), KindOfParameter(typeof(T3)), KindOfParameter(typeof(T4))],
            resultKind: KindOfParameter(typeof(TResult)));
    }

    public void SetFunction<T1, T2, T3, T4, T5, TResult>(string name, Func<T1, T2, T3, T4, T5, TResult> func) {
        SetFunction(name, FunctionWrapper.Wrap(name, func),
            paramKinds: [KindOfParameter(typeof(T1)), KindOfParameter(typeof(T2)), KindOfParameter(typeof(T3)), KindOfParameter(typeof(T4)), KindOfParameter(typeof(T5))],
            resultKind: KindOfParameter(typeof(TResult)));
    }

    public void SetFunction<T1, T2, T3, T4, T5, T6, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, TResult> func) {
        SetFunction(name, FunctionWrapper.Wrap(name, func),
            paramKinds: [KindOfParameter(typeof(T1)), KindOfParameter(typeof(T2)), KindOfParameter(typeof(T3)), KindOfParameter(typeof(T4)), KindOfParameter(typeof(T5)), KindOfParameter(typeof(T6))],
            resultKind: KindOfParameter(typeof(TResult)));
    }

    public void SetFunction<T1, T2, T3, T4, T5, T6, T7, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, TResult> func) {
        SetFunction(name, FunctionWrapper.Wrap(name, func),
            paramKinds: [KindOfParameter(typeof(T1)), KindOfParameter(typeof(T2)), KindOfParameter(typeof(T3)), KindOfParameter(typeof(T4)), KindOfParameter(typeof(T5)), KindOfParameter(typeof(T6)), KindOfParameter(typeof(T7))],
            resultKind: KindOfParameter(typeof(TResult)));
    }

    public void SetFunction<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> func) {
        SetFunction(name, FunctionWrapper.Wrap(name, func),
            paramKinds: [KindOfParameter(typeof(T1)), KindOfParameter(typeof(T2)), KindOfParameter(typeof(T3)), KindOfParameter(typeof(T4)), KindOfParameter(typeof(T5)), KindOfParameter(typeof(T6)), KindOfParameter(typeof(T7)), KindOfParameter(typeof(T8))],
            resultKind: KindOfParameter(typeof(TResult)));
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

    public ExpressionContext CreateChild() {
        return new ExpressionContext(this);
    }

    public void RemoveSymbol(string name) {
        if (_symbols.TryRemove(name, out _)) _symbolVersion++;
    }

    public void RemoveFunction(string name) {
        if (_functions.TryRemove(name, out _)) _symbolVersion++;
    }

    /// <summary>
    /// 静态探测符号的 Kind（不触发延迟值求值）：
    /// 直接值 → 按 <see cref="MathValue.TryKindOf"/> 探测；延迟值/未知类型 → null（无法静态确定）。
    /// 找不到符号时返回 false（与 TryGetSymbol 的查找链一致）。供 KindInferencePass 使用
    /// </summary>
    internal bool TryGetSymbolKind(string name, out MathKind? kind) {
        if (_symbols.TryGetValue(name, out var entry)) {
            kind = entry.IsLazy ? null : MathValue.TryKindOf(entry.DirectValue);
            return true;
        }

        if (_parent != null) return _parent.TryGetSymbolKind(name, out kind);

        if (s_builtInSymbols.ContainsKey(name)) {
            kind = MathKind.Number;
            return true;
        }

        kind = null;
        return false;
    }

    /// <summary>
    /// .NET 类型 → Kind 签名：数值族映射 Number（可从 double 经 Convert 转换），
    /// string/char → Text，一维数组 → 数组 Kind；
    /// bool/object/自定义类型返回 null（不约束，运行时兜底检查）
    /// </summary>
    private static MathKind? KindOfParameter(Type type) {
        if (type == typeof(double) || type == typeof(float) || type == typeof(long) || type == typeof(int)
            || type == typeof(short) || type == typeof(sbyte) || type == typeof(byte) || type == typeof(ushort)
            || type == typeof(uint) || type == typeof(ulong) || type == typeof(decimal))
            return MathKind.Number;
        if (type == typeof(string) || type == typeof(char)) return MathKind.Text;
        if (type == typeof(double[])) return MathKind.NumberArray;
        if (type == typeof(string[])) return MathKind.TextArray;
        return null;
    }

    private class SymbolEntry {
        public object? DirectValue { get; init; }
        public Func<object>? LazyValue { get; init; }
        public bool IsLazy => LazyValue != null;

        public object GetValue() => IsLazy ? LazyValue!() : DirectValue!;
    }
}
