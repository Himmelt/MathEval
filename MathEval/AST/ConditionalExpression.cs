using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示三元条件表达式
/// </summary>
public class ConditionalExpression : LogicalExpression
{
    /// <summary>
    /// 获取条件表达式
    /// </summary>
    public LogicalExpression Condition { get; }

    /// <summary>
    /// 获取条件为 true 时的表达式
    /// </summary>
    public LogicalExpression TrueExpression { get; }

    /// <summary>
    /// 获取条件为 false 时的表达式
    /// </summary>
    public LogicalExpression FalseExpression { get; }

    /// <summary>
    /// 初始化 ConditionalExpression 类的新实例
    /// </summary>
    /// <param name="condition">条件表达式</param>
    /// <param name="trueExpression">条件为 true 时的表达式</param>
    /// <param name="falseExpression">条件为 false 时的表达式</param>
    public ConditionalExpression(LogicalExpression condition, LogicalExpression trueExpression, LogicalExpression falseExpression)
    {
        Condition = condition;
        TrueExpression = trueExpression;
        FalseExpression = falseExpression;
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
