using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示标识符表达式
/// </summary>
/// <remarks>
/// 初始化 Identifier 类的新实例
/// </remarks>
/// <param name="name">标识符名称</param>
public class Identifier(string name) : LogicalExpression {
    /// <summary>
    /// 获取标识符名称
    /// </summary>
    public string Name { get; } = name;

    /// <inheritdoc />
    public override void Accept(IExpressionVisitor visitor) {
        visitor.Visit(this);
    }

    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) {
        return visitor.Visit(this);
    }
}
