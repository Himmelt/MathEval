namespace MathEval.Exceptions;

/// <summary>
/// 表示调用用户自定义函数时，其委托内部抛出了异常。
/// 原异常保留在 <see cref="Exception.InnerException"/> 中。
/// </summary>
public class FunctionInvocationException : EvaluationException {
    /// <summary>函数名称</summary>
    public string FunctionName { get; }

    /// <summary>初始化 FunctionInvocationException 类的新实例</summary>
    /// <param name="functionName">函数名称</param>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">用户委托内部抛出的异常</param>
    public FunctionInvocationException(string functionName, string message, Exception innerException)
        : base(MathEvalErrorCode.FunctionInvocation, message, innerException) {
        FunctionName = functionName;
    }
}
