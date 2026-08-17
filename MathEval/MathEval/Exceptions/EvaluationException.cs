namespace MathEval.Exceptions;

/// <summary>
/// 表示表达式求值过程中发生的运行期错误（原 EvaluateException）。
/// 兜底类别，细分错误通过 <see cref="MathEvalException.Code"/> 区分。
/// </summary>
public class EvaluationException : MathEvalException {
    /// <summary>初始化 EvaluationException 类的新实例</summary>
    /// <param name="code">错误码</param>
    /// <param name="message">异常消息</param>
    public EvaluationException(MathEvalErrorCode code, string message) : base(code, message) { }

    /// <summary>初始化 EvaluationException 类的新实例</summary>
    /// <param name="code">错误码</param>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    public EvaluationException(MathEvalErrorCode code, string message, Exception innerException)
        : base(code, message, innerException) { }
}
