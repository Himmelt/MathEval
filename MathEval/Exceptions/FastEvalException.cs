namespace MathEval.Exceptions;

/// <summary>
/// FastEval 快速求值器异常
/// </summary>
/// <remarks>
/// 初始化 FastEvalException 类的新实例
/// </remarks>
public class FastEvalException(string message, int position = -1) : MathEvalException(position >= 0 ? $"{message}，位置 {position}" : message) {
    /// <summary>
    /// 错误位置（字符偏移量），-1 表示未知
    /// </summary>
    public int Position { get; } = position;
}