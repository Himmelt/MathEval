namespace MathEval.Exceptions;

/// <summary>
/// 表示函数参数类型不匹配
/// </summary>
/// <remarks>
/// 初始化 FunctionTypeMismatchException 类的新实例
/// </remarks>
/// <param name="message">异常消息</param>
public class FunctionTypeMismatchException(string message) : EvaluateException(message) {
}
