namespace MathEval.Exceptions;

/// <summary>
/// 表示除零错误
/// </summary>
public class DivisionByZeroException : EvaluateException
{
    /// <summary>
    /// 初始化 DivisionByZeroException 类的新实例
    /// </summary>
    public DivisionByZeroException() : base("Division by zero")
    {
    }
}
