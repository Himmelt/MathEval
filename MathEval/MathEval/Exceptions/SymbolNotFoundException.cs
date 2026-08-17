namespace MathEval.Exceptions;

/// <summary>表示找不到指定的符号</summary>
public class SymbolNotFoundException : ResolutionException {
    /// <summary>初始化 SymbolNotFoundException 类的新实例</summary>
    /// <param name="symbolName">符号名称</param>
    public SymbolNotFoundException(string symbolName)
        : base(MathEvalErrorCode.SymbolNotFound, $"未找到符号 '{symbolName}'", symbolName) { }
}
