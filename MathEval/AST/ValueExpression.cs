using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示常量值表达式
/// </summary>
/// <remarks>
/// 初始化 ValueExpression 类的新实例
/// </remarks>
/// <param name="value">值</param>
public sealed class ValueExpression(object value) : LogicalExpression {
    /// <summary>
    /// 获取值
    /// </summary>
    public object Value { get; } = value;

    /// <inheritdoc />
    public override void Accept(IExpressionVisitor visitor) {
        visitor.Visit(this);
    }

    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) {
        return visitor.Visit(this);
    }
}
