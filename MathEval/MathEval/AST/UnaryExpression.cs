using MathEval.Parser;
using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示一元表达式
/// </summary>
/// <remarks>
/// 初始化 UnaryExpression 类的新实例
/// </remarks>
/// <param name="type">一元表达式类型</param>
/// <param name="operand">操作数</param>
public sealed class UnaryExpression(UnaryExpressionType type, LogicalExpression operand) : LogicalExpression {
    /// <summary>
    /// 获取一元表达式类型
    /// </summary>
    public UnaryExpressionType Type { get; } = type;

    /// <summary>
    /// 获取操作数
    /// </summary>
    public LogicalExpression Operand { get; } = operand;

    /// <inheritdoc />
    public override void Accept(IExpressionVisitor visitor) {
        visitor.Visit(this);
    }

    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) {
        return visitor.Visit(this);
    }
}
