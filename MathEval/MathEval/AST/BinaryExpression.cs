using MathEval.Parser;
using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示二元表达式
/// </summary>
/// <remarks>
/// 初始化 BinaryExpression 类的新实例
/// </remarks>
/// <param name="type">二元表达式类型</param>
/// <param name="left">左操作数</param>
/// <param name="right">右操作数</param>
public sealed class BinaryExpression(BinaryExpressionType type, LogicalExpression left, LogicalExpression right) : LogicalExpression {
    /// <summary>
    /// 获取二元表达式类型
    /// </summary>
    public BinaryExpressionType Type { get; } = type;

    /// <summary>
    /// 获取左操作数
    /// </summary>
    public LogicalExpression Left { get; } = left;

    /// <summary>
    /// 获取右操作数
    /// </summary>
    public LogicalExpression Right { get; } = right;

    /// <inheritdoc />
    public override void Accept(IExpressionVisitor visitor) {
        visitor.Visit(this);
    }

    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) {
        return visitor.Visit(this);
    }
}
