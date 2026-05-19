namespace MathEval.Lexer;

/// <summary>
/// 表示词法分析器生成的令牌
/// </summary>
/// <remarks>
/// 初始化 Token 类的新实例
/// </remarks>
/// <param name="type">令牌类型</param>
/// <param name="text">原始文本</param>
/// <param name="position">起始位置</param>
/// <param name="line">行号</param>
/// <param name="column">列号</param>
public class Token(TokenType type, string text, int position, int line, int column) {
    /// <summary>
    /// 获取令牌类型
    /// </summary>
    public TokenType Type { get; } = type;

    /// <summary>
    /// 获取令牌的原始文本
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    /// 获取令牌在表达式中的起始位置
    /// </summary>
    public int Position { get; } = position;

    /// <summary>
    /// 获取令牌在表达式中的行号
    /// </summary>
    public int Line { get; } = line;

    /// <summary>
    /// 获取令牌在表达式中的列号
    /// </summary>
    public int Column { get; } = column;

    /// <summary>
    /// 返回表示当前对象的字符串
    /// </summary>
    public override string ToString() {
        return $"Token({Type}, '{Text}', Position: {Position}, Line: {Line}, Column: {Column})";
    }
}
