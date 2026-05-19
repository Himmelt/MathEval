namespace MathEval;

/// <summary>
/// 表示表达式计算选项
/// </summary>
[Flags]
public enum ExpressionOptions {
    /// <summary>
    /// 无选项
    /// </summary>
    None = 0,

    /// <summary>
    /// 禁用表达式缓存
    /// </summary>
    NoCache = 1
}