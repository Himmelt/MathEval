using MathEval.Fast.Exceptions;
using System.Diagnostics.CodeAnalysis;

namespace MathEval.Fast.BuiltIn;

/// <summary>
/// FastEval 内置函数表，硬编码常用数学函数
/// </summary>
internal static class BuiltInFunctions {

    private static readonly Dictionary<string, Func<double[], double>> _functions = new() {
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
    };

    private static Func<double[], double> Func(string name, int argCount, Func<double[], double> fn) => args => args.Length == argCount ? fn(args) : throw new FastEvalException($"函数 {name} 需要 {argCount} 个参数，但提供了 {args.Length} 个");

    private static Func<double[], double> Func(string name, int minArgs, int maxArgs, Func<double[], double> fn) => args => args.Length >= minArgs && args.Length <= maxArgs ? fn(args) : throw new FastEvalException($"函数 {name} 需要 {minArgs}-{(maxArgs == int.MaxValue ? "∞" : maxArgs.ToString())} 个参数，但提供了 {args.Length} 个");

    public static bool TryGetFunction(string name, [NotNullWhen(true)] out Func<double[], double>? func) => _functions.TryGetValue(name, out func);

    /// <summary>
    /// Span 版本函数查找，按长度分组快速匹配，零字符串分配
    /// </summary>
    public static bool TryGetFunction(ReadOnlySpan<char> name, [NotNullWhen(true)] out Func<double[], double>? func) {
        // 按长度分组，减少比较次数
        switch (name.Length) {
            case 2:
                if (EqualsLower(name, "ln")) { func = _functions["ln"]; return true; }
                if (EqualsLower(name, "lg")) { func = _functions["lg"]; return true; }
                break;
            case 3:
                if (EqualsLower(name, "sin")) { func = _functions["sin"]; return true; }
                if (EqualsLower(name, "cos")) { func = _functions["cos"]; return true; }
                if (EqualsLower(name, "tan")) { func = _functions["tan"]; return true; }
                if (EqualsLower(name, "exp")) { func = _functions["exp"]; return true; }
                if (EqualsLower(name, "pow")) { func = _functions["pow"]; return true; }
                if (EqualsLower(name, "abs")) { func = _functions["abs"]; return true; }
                if (EqualsLower(name, "log")) { func = _functions["log"]; return true; }
                if (EqualsLower(name, "max")) { func = _functions["max"]; return true; }
                if (EqualsLower(name, "min")) { func = _functions["min"]; return true; }
                break;
            case 4:
                if (EqualsLower(name, "asin")) { func = _functions["asin"]; return true; }
                if (EqualsLower(name, "acos")) { func = _functions["acos"]; return true; }
                if (EqualsLower(name, "atan")) { func = _functions["atan"]; return true; }
                if (EqualsLower(name, "sqrt")) { func = _functions["sqrt"]; return true; }
                if (EqualsLower(name, "sign")) { func = _functions["sign"]; return true; }
                if (EqualsLower(name, "ceil")) { func = _functions["ceil"]; return true; }
                if (EqualsLower(name, "log2")) { func = _functions["log2"]; return true; }
                break;
            case 5:
                if (EqualsLower(name, "atan2")) { func = _functions["atan2"]; return true; }
                if (EqualsLower(name, "floor")) { func = _functions["floor"]; return true; }
                if (EqualsLower(name, "trunc")) { func = _functions["trunc"]; return true; }
                if (EqualsLower(name, "round")) { func = _functions["round"]; return true; }
                if (EqualsLower(name, "log10")) { func = _functions["log10"]; return true; }
                break;
        }
        func = null;
        return false;
    }

    private static bool EqualsLower(ReadOnlySpan<char> span, string keyword) {
        if (span.Length != keyword.Length) return false;
        for (int i = 0; i < keyword.Length; i++) {
            if (char.ToLowerInvariant(span[i]) != char.ToLowerInvariant(keyword[i])) return false;
        }
        return true;
    }
}
