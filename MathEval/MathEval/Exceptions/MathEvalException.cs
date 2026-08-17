namespace MathEval.Exceptions;

/// <summary>
/// MathEval 库中所有表达式相关异常（L0 表达式契约层）的抽象基类。
/// API 误用（参数非法）与库内部 bug 不继承本类，分别使用 BCL 的
/// <see cref="System.ArgumentException"/> 族与 <see cref="System.InvalidOperationException"/>。
/// </summary>
public abstract class MathEvalException : Exception {
    /// <summary>机器可读错误码</summary>
    public MathEvalErrorCode Code { get; }

    /// <summary>
    /// 错误在表达式源码中的字符偏移（0-based）。-1 表示无法定位到源码。
    /// </summary>
    public int Position { get; }

    /// <summary>初始化 MathEvalException 类的新实例</summary>
    /// <param name="code">错误码</param>
    /// <param name="message">异常消息</param>
    protected MathEvalException(MathEvalErrorCode code, string message) : base(message) {
        Code = code;
        Position = -1;
    }

    /// <summary>初始化 MathEvalException 类的新实例</summary>
    /// <param name="code">错误码</param>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    protected MathEvalException(MathEvalErrorCode code, string message, Exception innerException)
        : base(message, innerException) {
        Code = code;
        Position = -1;
    }

    /// <summary>初始化 MathEvalException 类的新实例</summary>
    /// <param name="code">错误码</param>
    /// <param name="message">异常消息</param>
    /// <param name="position">错误在表达式中的字符偏移（0-based，-1 表示未知）</param>
    protected MathEvalException(MathEvalErrorCode code, string message, int position) : base(message) {
        Code = code;
        Position = position;
    }

    /// <summary>初始化 MathEvalException 类的新实例</summary>
    /// <param name="code">错误码</param>
    /// <param name="message">异常消息</param>
    /// <param name="position">错误在表达式中的字符偏移（0-based，-1 表示未知）</param>
    /// <param name="innerException">内部异常</param>
    protected MathEvalException(MathEvalErrorCode code, string message, int position, Exception innerException)
        : base(message, innerException) {
        Code = code;
        Position = position;
    }
}
