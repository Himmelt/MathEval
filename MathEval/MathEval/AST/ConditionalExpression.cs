using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示三元条件表达式
/// </summary>
/// <remarks>
/// 初始化 ConditionalExpression 类的新实例
/// </remarks>
/// <param name="condition">条件表达式</param>
/// <param name="trueExpression">条件为 true 时的表达式</param>
/// <param name="falseExpression">条件为 false 时的表达式</param>
public sealed class ConditionalExpression(LogicalExpression condition, LogicalExpression trueExpression, LogicalExpression falseExpression) : LogicalExpression {
    /// <summary>
    /// 获取条件表达式
    /// </summary>
    public LogicalExpression Condition { get; } = condition;

    /// <summary>
    /// 获取条件为 true 时的表达式
    /// </summary>
    public LogicalExpression TrueExpression { get; } = trueExpression;

    /// <summary>
    /// 获取条件为 false 时的表达式
    /// </summary>
    public LogicalExpression FalseExpression { get; } = falseExpression;

    /// <inheritdoc />
    public override void Accept(IExpressionVisitor visitor) {
        visitor.Visit(this);
    }

    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) {
        return visitor.Visit(this);
    }
}
