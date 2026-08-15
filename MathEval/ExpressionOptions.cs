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
    NoCache = 1,

    /// <summary>
    /// 启用常量折叠优化
    /// </summary>
    ConstantFolding = 2,

    /// <summary>
    /// 启用编译优化（将 AST 编译为委托）
    /// </summary>
    CompileOptimization = 4,

    /// <summary>
    /// 禁用索引下推优化（默认启用）。索引下推默认开启，设置此选项可关闭。
    /// </summary>
    DisableIndexPushdown = 8,

    /// <summary>
    /// 启用静态 Kind 推断：求值前对整棵 AST 做一遍类型检查，
    /// 无效的类型组合在执行前抛出（含死分支）；整棵树为纯 Number 时编译为特化代码
    /// </summary>
    StrictTypes = 16
}