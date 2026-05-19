using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示常量值表达式
/// </summary>
public class ValueExpression : LogicalExpression
{
    /// <summary>
    /// 获取值
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// 初始化 ValueExpression 类的新实例
    /// </summary>
    /// <param name="value">值</param>
    public ValueExpression(object value)
    {
        Value = value;
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
