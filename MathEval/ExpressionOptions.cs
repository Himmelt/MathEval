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
    CompileOptimization = 4
}