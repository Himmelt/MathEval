namespace MathEval.Exceptions;

/// <summary>
/// 表示名字解析失败的抽象基类（符号或函数未定义），承载统一的名字属性。
/// </summary>
public abstract class ResolutionException : MathEvalException {
    /// <summary>未找到的名字（符号名或函数名）</summary>
    public string Name { get; }

    /// <summary>初始化 ResolutionException 类的新实例</summary>
    /// <param name="code">错误码</param>
    /// <param name="message">异常消息</param>
    /// <param name="name">未找到的名字</param>
    protected ResolutionException(MathEvalErrorCode code, string message, string name)
        : base(code, message) {
        Name = name;
    }
}
