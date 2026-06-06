using System.Diagnostics.CodeAnalysis;

namespace MathEval.Fast;

/// <summary>
/// FastEval 内置函数表，硬编码常用数学函数
/// </summary>
internal static class BuiltInFastFunctions {
    private static readonly Dictionary<string, Func<double[], double>> _functions = new(StringComparer.OrdinalIgnoreCase) {
        ["sin"] = args => Math.Sin(args[0]),
        ["cos"] = args => Math.Cos(args[0]),
        ["tan"] = args => Math.Tan(args[0]),
        ["asin"] = args => Math.Asin(args[0]),
        ["acos"] = args => Math.Acos(args[0]),
        ["atan"] = args => Math.Atan(args[0]),
        ["atan2"] = args => Math.Atan2(args[0], args[1]),
        ["sqrt"] = args => Math.Sqrt(args[0]),
        ["abs"] = args => Math.Abs(args[0]),
        ["exp"] = args => Math.Exp(args[0]),
        ["ln"] = args => Math.Log(args[0]),
        ["log"] = args => Math.Log(args[0]),
        ["log10"] = args => Math.Log10(args[0]),
        ["log2"] = args => Math.Log2(args[0]),
        ["ceil"] = args => Math.Ceiling(args[0]),
        ["floor"] = args => Math.Floor(args[0]),
        ["round"] = args => args.Length == 1 ? Math.Round(args[0]) : Math.Round(args[0], (int)args[1]),
        ["trunc"] = args => Math.Truncate(args[0]),
        ["sign"] = args => Math.Sign(args[0]),
        ["max"] = args => Math.Max(args[0], args[1]),
        ["min"] = args => Math.Min(args[0], args[1]),
        ["pow"] = args => Math.Pow(args[0], args[1]),
    };

    public static bool TryGetFunction(string name, [NotNullWhen(true)] out Func<double[], double>? func) => _functions.TryGetValue(name, out func);
}