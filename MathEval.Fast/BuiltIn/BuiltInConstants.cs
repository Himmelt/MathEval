using System.Collections.Frozen;
using System.Runtime.CompilerServices;

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

    private static readonly FrozenDictionary<string, double>.AlternateLookup<ReadOnlySpan<char>> _spanLookup = _constants.GetAlternateLookup<ReadOnlySpan<char>>();

    public static bool TryGetValue(string name, out double value) => _constants.TryGetValue(name, out value);

    /// <summary>
    /// Span 版本常量查找，零字符串分配
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetValue(ReadOnlySpan<char> name, out double value) => _spanLookup.TryGetValue(name, out value);
}