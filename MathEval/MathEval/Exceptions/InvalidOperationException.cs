namespace MathEval.Exceptions;

/// <summary>
/// 表示无效的操作异常
/// </summary>
public class InvalidOperationException : EvaluateException {
    /// <summary>
    /// 初始化 InvalidOperationException 类的新实例
    /// </summary>
    /// <param name="message">异常消息</param>
    public InvalidOperationException(string message) : base(message) {
    }

    /// <summary>
    /// 初始化 InvalidOperationException 类的新实例
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    public InvalidOperationException(string message, Exception innerException) : base(message, innerException) {
    }
}
