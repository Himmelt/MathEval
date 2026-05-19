namespace MathEval.Exceptions;

/// <summary>
/// 表示数值溢出错误
/// </summary>
public class OverflowException : EvaluateException {
    /// <summary>
    /// 初始化 OverflowException 类的新实例
    /// </summary>
    /// <param name="message">异常消息</param>
    public OverflowException(string message) : base(message) {
    }

    /// <summary>
    /// 初始化 OverflowException 类的新实例
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    public OverflowException(string message, Exception innerException) : base(message, innerException) {
    }
}
