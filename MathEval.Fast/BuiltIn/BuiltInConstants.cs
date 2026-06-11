using System.Collections.Frozen;

namespace MathEval.Fast.BuiltIn;

/// <summary>
/// FastEval 内置常量表，包含数学常数和特殊值
/// </summary>
internal static class BuiltInConstants {

    private static readonly FrozenDictionary<string, double> _constants = new Dictionary<string, double>() {
        ["E"] = Math.E,
        ["π"] = Math.PI,
        ["PI"] = Math.PI,
        ["NaN"] = double.NaN,
        ["INF"] = double.PositiveInfinity,
        ["true"] = 1.0,
        ["false"] = 0.0,
    }.ToFrozenDictionary();

    public static bool TryGetValue(string name, out double value) => _constants.TryGetValue(name, out value);

    /// <summary>
    /// Span 版本常量查找（.NET 8 兼容版本）
    /// </summary>
    public static bool TryGetValue(ReadOnlySpan<char> name, out double value) {
        // .NET 8 需要将 Span 转换为 string
        return _constants.TryGetValue(name.ToString(), out value);
    }
}