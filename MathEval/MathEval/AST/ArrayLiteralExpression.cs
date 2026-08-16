using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示数组常量表达式，如 [1, 2, 3]、[1+2, sqrt(9)]
/// </summary>
public sealed class ArrayLiteralExpression : LogicalExpression {
    private readonly List<LogicalExpression> _elements;

    /// <summary>
    /// 初始化 ArrayLiteralExpression 类的新实例
    /// </summary>
    /// <param name="elements">数组元素表达式列表</param>
    public ArrayLiteralExpression(List<LogicalExpression> elements) {
        _elements = elements;
    }

    /// <summary>
    /// 获取数组元素表达式列表
    /// </summary>
    public IReadOnlyList<LogicalExpression> Elements => _elements;

    /// <inheritdoc />
    public override void Accept(IExpressionVisitor visitor) {
        visitor.Visit(this);
    }

    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) {
        return visitor.Visit(this);
    }
}
