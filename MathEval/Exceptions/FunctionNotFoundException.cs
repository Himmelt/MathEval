namespace MathEval.Exceptions;

/// <summary>
/// 表示找不到指定的函数
/// </summary>
/// <remarks>
/// 初始化 FunctionNotFoundException 类的新实例
/// </remarks>
/// <param name="functionName">函数名称</param>
public class FunctionNotFoundException(string functionName) : EvaluateException($"未找到函数 '{functionName}'") {
    /// <summary>
    /// 获取函数名称
    /// </summary>
    public string FunctionName { get; } = functionName;
}