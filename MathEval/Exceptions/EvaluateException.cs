namespace MathEval.Exceptions;

/// <summary>
/// 表示表达式求值过程中发生的错误
/// </summary>
public class EvaluateException : MathEvalException {
    /// <summary>
    /// 初始化 EvaluateException 类的新实例
    /// </summary>
    /// <param name="message">异常消息</param>
    public EvaluateException(string message) : base(message) { }

    /// <summary>
    /// 初始化 EvaluateException 类的新实例
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    public EvaluateException(string message, Exception innerException) : base(message, innerException) { }
}