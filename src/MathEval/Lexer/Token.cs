namespace MathEval.Lexer;

/// <summary>
/// 表示词法分析器生成的令牌
/// </summary>
public class Token
{
    /// <summary>
    /// 获取令牌类型
    /// </summary>
    public TokenType Type { get; }

    /// <summary>
    /// 获取令牌的原始文本
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 获取令牌在表达式中的起始位置
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// 获取令牌在表达式中的行号
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// 获取令牌在表达式中的列号
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// 初始化 Token 类的新实例
    /// </summary>
    /// <param name="type">令牌类型</param>
    /// <param name="text">原始文本</param>
    /// <param name="position">起始位置</param>
    /// <param name="line">行号</param>
    /// <param name="column">列号</param>
    public Token(TokenType type, string text, int position, int line, int column)
    {
        Type = type;
        Text = text;
        Position = position;
        Line = line;
        Column = column;
    }

    /// <summary>
    /// 返回表示当前对象的字符串
    /// </summary>
    public override string ToString()
    {
        return $"Token({Type}, '{Text}', Position: {Position}, Line: {Line}, Column: {Column})";
    }
}
