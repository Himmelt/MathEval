namespace MathEval.Context;

/// <summary>
/// 函数行为标记，用于指示函数对数组参数的处理方式
/// </summary>
[Flags]
public enum FunctionFlags {
    /// <summary>
    /// 无标记
    /// </summary>
    None = 0,

    /// <summary>
    /// 逐元素函数：对数组的每个元素独立操作（默认行为）
    /// 例如 sin([1,2,3]) → [sin(1), sin(2), sin(3)]
    /// </summary>
    ElementWise = 1,

    /// <summary>
    /// 聚合函数：将数组参数展平后全局归约
    /// 例如 max([1,2,3]) → 3
    /// </summary>
    Aggregate = 2,
}
