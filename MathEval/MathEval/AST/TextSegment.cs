namespace MathEval.AST;

/// <summary>
/// 表示插值字符串中的纯文本段
/// </summary>
/// <remarks>
/// 初始化 TextSegment 类的新实例
/// </remarks>
/// <param name="text">文本内容</param>
public class TextSegment(string text) : InterpolationSegment {
    /// <summary>
    /// 获取文本内容
    /// </summary>
    public string Text { get; } = text;
}
