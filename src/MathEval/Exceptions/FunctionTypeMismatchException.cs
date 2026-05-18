namespace MathEval.Exceptions;

/// <summary>
/// 表示函数参数类型不匹配
/// </summary>
public class FunctionTypeMismatchException : EvaluateException
{
    /// <summary>
    /// 初始化 FunctionTypeMismatchException 类的新实例
    /// </summary>
    /// <param name="message">异常消息</param>
    public FunctionTypeMismatchException(string message) : base(message)
    {
    }
}
