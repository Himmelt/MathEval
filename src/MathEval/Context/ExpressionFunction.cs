namespace MathEval.Context;

/// <summary>
/// 表示可注册到上下文的函数委托
/// </summary>
/// <param name="args">函数参数</param>
/// <returns>函数结果</returns>
public delegate object ExpressionFunction(object[] args);
