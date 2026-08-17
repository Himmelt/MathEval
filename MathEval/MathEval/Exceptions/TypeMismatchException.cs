namespace MathEval.Exceptions;

/// <summary>
/// 表示类型不匹配错误。携带期望类型与实际类型，供程序化诊断。
/// </summary>
public class TypeMismatchException : MathEvalException {
    /// <summary>期望的类型</summary>
    public string ExpectedType { get; }

    /// <summary>实际的类型</summary>
    public string ActualType { get; }

    /// <summary>初始化 TypeMismatchException 类的新实例</summary>
    /// <param name="message">异常消息</param>
    /// <param name="expectedType">期望的类型</param>
    /// <param name="actualType">实际的类型</param>
    public TypeMismatchException(string message, string expectedType, string actualType)
        : base(MathEvalErrorCode.TypeMismatch, message) {
        ExpectedType = expectedType;
        ActualType = actualType;
    }

    /// <summary>初始化 TypeMismatchException 类的新实例（指定错误码）</summary>
    /// <param name="code">错误码</param>
    /// <param name="message">异常消息</param>
    /// <param name="expectedType">期望的类型</param>
    /// <param name="actualType">实际的类型</param>
    public TypeMismatchException(MathEvalErrorCode code, string message, string expectedType, string actualType)
        : base(code, message) {
        ExpectedType = expectedType;
        ActualType = actualType;
    }

    /// <summary>初始化 TypeMismatchException 类的新实例</summary>
    /// <param name="message">异常消息</param>
    /// <param name="expectedType">期望的类型</param>
    /// <param name="actualType">实际的类型</param>
    /// <param name="innerException">内部异常</param>
    public TypeMismatchException(string message, string expectedType, string actualType, Exception innerException)
        : base(MathEvalErrorCode.TypeMismatch, message, innerException) {
        ExpectedType = expectedType;
        ActualType = actualType;
    }
}
