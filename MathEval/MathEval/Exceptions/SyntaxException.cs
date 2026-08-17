namespace MathEval.Exceptions;

/// <summary>
/// 表示表达式词法或语法错误（原 ParseException）。
/// 位置信息为源码字符偏移（0-based）。
/// </summary>
public class SyntaxException : MathEvalException {
    /// <summary>初始化 SyntaxException 类的新实例</summary>
    /// <param name="code">错误码</param>
    /// <param name="message">异常消息</param>
    /// <param name="position">错误在表达式中的字符偏移（0-based）</param>
    public SyntaxException(MathEvalErrorCode code, string message, int position)
        : base(code, message, position) { }

    /// <summary>初始化 SyntaxException 类的新实例</summary>
    /// <param name="code">错误码</param>
    /// <param name="message">异常消息</param>
    /// <param name="position">错误在表达式中的字符偏移（0-based）</param>
    /// <param name="innerException">内部异常</param>
    public SyntaxException(MathEvalErrorCode code, string message, int position, Exception innerException)
        : base(code, message, position, innerException) { }
}
