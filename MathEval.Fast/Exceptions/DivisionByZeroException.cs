namespace MathEval.Fast.Exceptions;

/// <summary>
/// 表示除零错误
/// </summary>
public class DivisionByZeroException : FastEvalException {
    /// <summary>
    /// 初始化 DivisionByZeroException 类的新实例
    /// </summary>
    public DivisionByZeroException() : base("除零错误") { }
}
