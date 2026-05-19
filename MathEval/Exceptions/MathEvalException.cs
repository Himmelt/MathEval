namespace MathEval.Exceptions;

/// <summary>
/// 基类异常，所有 MathEval 库中的异常都继承自此类
/// </summary>
public abstract class MathEvalException : Exception
{
    /// <summary>
    /// 初始化 MathEvalException 类的新实例
    /// </summary>
    /// <param name="message">异常消息</param>
    protected MathEvalException(string message) : base(message)
    {
    }

    /// <summary>
    /// 初始化 MathEvalException 类的新实例
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    protected MathEvalException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
