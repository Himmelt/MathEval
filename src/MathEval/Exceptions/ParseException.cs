namespace MathEval.Exceptions;

/// <summary>
/// 表示表达式解析过程中发生的错误
/// </summary>
public class ParseException : MathEvalException
{
    /// <summary>
    /// 获取表达式中发生错误的行号
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// 获取表达式中发生错误的列号
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// 初始化 ParseException 类的新实例
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="line">行号</param>
    /// <param name="column">列号</param>
    public ParseException(string message, int line, int column) : base(message)
    {
        Line = line;
        Column = column;
    }

    /// <summary>
    /// 初始化 ParseException 类的新实例
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="line">行号</param>
    /// <param name="column">列号</param>
    /// <param name="innerException">内部异常</param>
    public ParseException(string message, int line, int column, Exception innerException) 
        : base(message, innerException)
    {
        Line = line;
        Column = column;
    }
}
