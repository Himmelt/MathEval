namespace MathEval.AST;

/// <summary>
/// 表示插值字符串中的纯文本段
/// </summary>
public class TextSegment : InterpolationSegment
{
    /// <summary>
    /// 获取文本内容
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 初始化 TextSegment 类的新实例
    /// </summary>
    /// <param name="text">文本内容</param>
    public TextSegment(string text)
    {
        Text = text;
    }
}
