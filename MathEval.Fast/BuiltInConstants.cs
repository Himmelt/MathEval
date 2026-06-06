namespace MathEval.Fast;

/// <summary>
/// FastEval 内置常量表，包含数学常数和特殊值
/// </summary>
internal static class BuiltInConstants
{
    private static readonly Dictionary<string, double> _constants = new(StringComparer.OrdinalIgnoreCase)
    {
        ["true"]  = 1.0,
        ["false"] = 0.0,
        ["NaN"]   = double.NaN,
        ["INF"]   = double.PositiveInfinity,
        ["PI"]    = Math.PI,
        ["π"]     = Math.PI,
        ["E"]     = Math.E,
    };

    /// <summary>
    /// 尝试根据名称获取常量值，大小写不敏感
    /// </summary>
    public static bool TryGetValue(ReadOnlySpan<char> name, out double value)
        => _constants.TryGetValue(name.ToString(), out value);
}
