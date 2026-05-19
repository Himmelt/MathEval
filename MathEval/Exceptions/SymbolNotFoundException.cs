namespace MathEval.Exceptions;

/// <summary>
/// 表示找不到指定的符号
/// </summary>
/// <remarks>
/// 初始化 SymbolNotFoundException 类的新实例
/// </remarks>
/// <param name="symbolName">符号名称</param>
public class SymbolNotFoundException(string symbolName) : EvaluateException($"未找到符号 '{symbolName}'") {
    /// <summary>
    /// 获取符号名称
    /// </summary>
    public string SymbolName { get; } = symbolName;
}
