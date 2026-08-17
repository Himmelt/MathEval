namespace MathEval.Exceptions;

/// <summary>
/// 表示函数某个参数的类型不匹配。继承 <see cref="TypeMismatchException"/>，
/// 因此可被 <c>catch (TypeMismatchException)</c> 统一捕获。
/// </summary>
public class FunctionArgumentTypeException : TypeMismatchException {
    /// <summary>函数名称</summary>
    public string FunctionName { get; }

    /// <summary>不匹配的参数下标（0-based），-1 表示未知</summary>
    public int ArgumentIndex { get; }

    /// <summary>初始化 FunctionArgumentTypeException 类的新实例</summary>
    /// <param name="functionName">函数名称</param>
    /// <param name="argumentIndex">不匹配的参数下标（0-based）</param>
    /// <param name="expectedType">期望的类型</param>
    /// <param name="actualType">实际的类型</param>
    public FunctionArgumentTypeException(string functionName, int argumentIndex, string expectedType, string actualType)
        : base(MathEvalErrorCode.FunctionArgumentType,
               $"函数 {functionName} 第 {argumentIndex + 1} 个参数类型不匹配：期望 {expectedType}，实际为 {actualType}",
               expectedType, actualType) {
        FunctionName = functionName;
        ArgumentIndex = argumentIndex;
    }

    /// <summary>初始化 FunctionArgumentTypeException 类的新实例（仅消息，无结构化类型信息）</summary>
    /// <param name="functionName">函数名称</param>
    /// <param name="argumentIndex">不匹配的参数下标（0-based），-1 表示未知</param>
    /// <param name="message">异常消息</param>
    public FunctionArgumentTypeException(string functionName, int argumentIndex, string message)
        : base(MathEvalErrorCode.FunctionArgumentType, message, string.Empty, string.Empty) {
        FunctionName = functionName;
        ArgumentIndex = argumentIndex;
    }
}
