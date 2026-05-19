namespace MathEval.Exceptions;

/// <summary>
/// 表示类型不匹配错误
/// </summary>
public class TypeMismatchException : MathEvalException
{
    /// <summary>
    /// 获取期望的类型
    /// </summary>
    public string ExpectedType { get; }

    /// <summary>
    /// 获取实际的类型
    /// </summary>
    public string ActualType { get; }

    /// <summary>
    /// 初始化 TypeMismatchException 类的新实例
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="expectedType">期望的类型</param>
    /// <param name="actualType">实际的类型</param>
    public TypeMismatchException(string message, string expectedType, string actualType) : base(message)
    {
        ExpectedType = expectedType;
        ActualType = actualType;
    }
}
