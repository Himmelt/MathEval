using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示标识符表达式
/// </summary>
public class Identifier : LogicalExpression
{
    /// <summary>
    /// 获取标识符名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 初始化 Identifier 类的新实例
    /// </summary>
    /// <param name="name">标识符名称</param>
    public Identifier(string name)
    {
        Name = name;
    }

    /// <inheritdoc />
    public override void Accept(IExpressionVisitor visitor)
    {
        visitor.Visit(this);
    }

    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.Visit(this);
    }
}
