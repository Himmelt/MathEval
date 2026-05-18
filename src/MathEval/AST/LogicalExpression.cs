using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示抽象语法树节点的基类
/// </summary>
public abstract class LogicalExpression
{
    /// <summary>
    /// 接受访问者访问
    /// </summary>
    /// <param name="visitor">访问者</param>
    public abstract void Accept(IExpressionVisitor visitor);

    /// <summary>
    /// 接受访问者访问并返回结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="visitor">访问者</param>
    /// <returns>访问结果</returns>
    public abstract T Accept<T>(IExpressionVisitor<T> visitor);
}
