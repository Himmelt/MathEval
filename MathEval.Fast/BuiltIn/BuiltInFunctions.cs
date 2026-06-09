using MathEval.Fast.Exceptions;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace MathEval.Fast.BuiltIn;

/// <summary>
/// FastEval 内置函数表，硬编码常用数学函数
/// </summary>
internal static class BuiltInFunctions {

    private static readonly FrozenDictionary<string, Func<double[], double>> _functions = new Dictionary<string, Func<double[], double>>() {
        // 三角函数
        ["sin"] = Func("sin", 1, args => Math.Sin(args[0])),
        ["cos"] = Func("cos", 1, args => Math.Cos(args[0])),
        ["tan"] = Func("tan", 1, args => Math.Tan(args[0])),
        ["asin"] = Func("asin", 1, args => Math.Asin(args[0])),
        ["acos"] = Func("acos", 1, args => Math.Acos(args[0])),
        ["atan"] = Func("atan", 1, args => Math.Atan(args[0])),
        ["atan2"] = Func("atan2", 2, args => Math.Atan2(args[0], args[1])),
        // 指数幂函数
        ["exp"] = Func("exp", 1, args => Math.Exp(args[0])),
        ["pow"] = Func("pow", 2, args => Math.Pow(args[0], args[1])),
        // 对数函数
        ["ln"] = Func("ln", 1, args => Math.Log(args[0])),
        ["lg"] = Func("lg", 1, args => Math.Log10(args[0])),
        ["log"] = Func("log", 1, 2, args => args.Length == 1 ? Math.Log(args[0]) : Math.Log(args[0], args[1])),
        ["log2"] = Func("log2", 1, args => Math.Log2(args[0])),
        ["log10"] = Func("log10", 1, args => Math.Log10(args[0])),
        // 数值处理函数
        ["abs"] = Func("abs", 1, args => Math.Abs(args[0])),
        ["sqrt"] = Func("sqrt", 1, args => Math.Sqrt(args[0])),
        ["sign"] = Func("sign", 1, args => Math.Sign(args[0])),
        // 取整函数
        ["ceil"] = Func("ceil", 1, args => Math.Ceiling(args[0])),
        ["floor"] = Func("floor", 1, args => Math.Floor(args[0])),
        ["trunc"] = Func("trunc", 1, args => Math.Truncate(args[0])),
        ["round"] = Func("round", 1, 2, args => args.Length == 1 ? Math.Round(args[0]) : Math.Round(args[0], (int)args[1])),
        // 聚合函数
        ["max"] = Func("max", 1, int.MaxValue, args => args.Max()),
        ["min"] = Func("min", 1, int.MaxValue, args => args.Min()),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, Func<double[], double>>.AlternateLookup<ReadOnlySpan<char>> _spanLookup = _functions.GetAlternateLookup<ReadOnlySpan<char>>();

    private static Func<double[], double> Func(string name, int argCount, Func<double[], double> fn) => args => args.Length == argCount ? fn(args) : throw new FastEvalException($"函数 {name} 需要 {argCount} 个参数，但提供了 {args.Length} 个");

    private static Func<double[], double> Func(string name, int minArgs, int maxArgs, Func<double[], double> fn) => args => args.Length >= minArgs && args.Length <= maxArgs ? fn(args) : throw new FastEvalException($"函数 {name} 需要 {minArgs}-{(maxArgs == int.MaxValue ? "N" : maxArgs.ToString())} 个参数，但提供了 {args.Length} 个");

    public static bool TryGetFunction(string name, [NotNullWhen(true)] out Func<double[], double>? func) => _functions.TryGetValue(name, out func);

    /// <summary>
    /// Span 版本函数查找，零字符串分配
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetFunction(ReadOnlySpan<char> name, [NotNullWhen(true)] out Func<double[], double>? func) => _spanLookup.TryGetValue(name, out func);
}