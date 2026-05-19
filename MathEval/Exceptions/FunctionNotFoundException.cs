namespace MathEval.Exceptions;

/// <summary>
/// 表示找不到指定的函数
/// </summary>
public class FunctionNotFoundException : EvaluateException
{
    /// <summary>
    /// 获取函数名称
    /// </summary>
    public string FunctionName { get; }

    /// <summary>
    /// 初始化 FunctionNotFoundException 类的新实例
    /// </summary>
    /// <param name="functionName">函数名称</param>
    public FunctionNotFoundException(string functionName) 
        : base($"未找到函数 '{functionName}'")
    {
        FunctionName = functionName;
    }
}
