using MathEval.Exceptions;
using System.Diagnostics.CodeAnalysis;

namespace MathEval.Fast;

/// <summary>
/// FastEval 内置函数表，硬编码常用数学函数
/// </summary>
internal static class BuiltInFastFunctions {
    private static readonly Dictionary<string, Func<double[], double>> _doubleFunctions = new(StringComparer.OrdinalIgnoreCase) {
        ["sin"] = args => Math.Sin(args[0]),
        ["cos"] = args => Math.Cos(args[0]),
        ["tan"] = args => Math.Tan(args[0]),
        ["asin"] = args => Math.Asin(args[0]),
        ["acos"] = args => Math.Acos(args[0]),
        ["atan"] = args => Math.Atan(args[0]),
        ["atan2"] = args => Math.Atan2(args[0], args[1]),
        ["sqrt"] = args => args[0] < 0
            ? throw new FastEvalException("不能对负数求平方根")
            : Math.Sqrt(args[0]),
        ["abs"] = args => Math.Abs(args[0]),
        ["exp"] = args => Math.Exp(args[0]),
        ["ln"] = args => args[0] <= 0
            ? throw new FastEvalException("不能对非正数求对数")
            : Math.Log(args[0]),
        ["log"] = args => args[0] <= 0
            ? throw new FastEvalException("不能对非正数求对数")
            : Math.Log(args[0]),
        ["log10"] = args => args[0] <= 0
            ? throw new FastEvalException("不能对非正数求对数")
            : Math.Log10(args[0]),
        ["log2"] = args => args[0] <= 0
            ? throw new FastEvalException("不能对非正数求对数")
            : Math.Log2(args[0]),
        ["ceil"] = args => Math.Ceiling(args[0]),
        ["floor"] = args => Math.Floor(args[0]),
        ["round"] = args => args.Length == 1
            ? Math.Round(args[0])
            : Math.Round(args[0], (int)args[1]),
        ["truncate"] = args => Math.Truncate(args[0]),
        ["sign"] = args => Math.Sign(args[0]),
        ["max"] = args => Math.Max(args[0], args[1]),
        ["min"] = args => Math.Min(args[0], args[1]),
        ["pow"] = args => args[0] < 0 && args[1] != Math.Floor(args[1])
            ? throw new FastEvalException("不能对负数求非整数次幂")
            : Math.Pow(args[0], args[1]),
    };

    private static readonly Dictionary<string, Func<long[], long>> _longFunctions = new(StringComparer.OrdinalIgnoreCase) {
        ["abs"] = args => Math.Abs(args[0]),
        ["sign"] = args => Math.Sign(args[0]),
        ["max"] = args => Math.Max(args[0], args[1]),
        ["min"] = args => Math.Min(args[0], args[1]),
    };

    public static bool TryGetDoubleFunction(string name, [NotNullWhen(true)] out Func<double[], double>? func)
        => _doubleFunctions.TryGetValue(name, out func);

    public static bool TryGetLongFunction(string name, [NotNullWhen(true)] out Func<long[], long>? func)
        => _longFunctions.TryGetValue(name, out func);
}