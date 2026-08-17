namespace MathEval.Exceptions;

/// <summary>表示函数参数个数不匹配</summary>
public class FunctionArityException : EvaluationException {
    /// <summary>函数名称</summary>
    public string FunctionName { get; }

    /// <summary>期望的最小参数个数</summary>
    public int ExpectedMin { get; }

    /// <summary>期望的最大参数个数（变参时为 <see cref="int.MaxValue"/>）</summary>
    public int ExpectedMax { get; }

    /// <summary>实际提供的参数个数</summary>
    public int ActualCount { get; }

    /// <summary>初始化 FunctionArityException 类的新实例（固定参数个数）</summary>
    /// <param name="functionName">函数名称</param>
    /// <param name="expectedCount">期望的参数个数</param>
    /// <param name="actualCount">实际提供的参数个数</param>
    public FunctionArityException(string functionName, int expectedCount, int actualCount)
        : base(MathEvalErrorCode.FunctionArity,
               $"函数 {functionName} 需要 {expectedCount} 个参数，但提供了 {actualCount} 个") {
        FunctionName = functionName;
        ExpectedMin = expectedCount;
        ExpectedMax = expectedCount;
        ActualCount = actualCount;
    }

    /// <summary>初始化 FunctionArityException 类的新实例（参数个数区间）</summary>
    /// <param name="functionName">函数名称</param>
    /// <param name="minArgs">最小参数个数</param>
    /// <param name="maxArgs">最大参数个数</param>
    /// <param name="actualCount">实际提供的参数个数</param>
    public FunctionArityException(string functionName, int minArgs, int maxArgs, int actualCount)
        : base(MathEvalErrorCode.FunctionArity,
               $"函数 {functionName} 需要 {minArgs}-{(maxArgs == int.MaxValue ? "∞" : maxArgs.ToString())} 个参数，但提供了 {actualCount} 个") {
        FunctionName = functionName;
        ExpectedMin = minArgs;
        ExpectedMax = maxArgs;
        ActualCount = actualCount;
    }
}
