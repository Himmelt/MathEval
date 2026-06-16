using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示数组索引表达式，如 arr[0]、arr[i]、(arr*2)[3]
/// </summary>
public class ArrayIndexExpression : LogicalExpression {
    /// <summary>
    /// 初始化 ArrayIndexExpression 类的新实例
    /// </summary>
    /// <param name="array">数组表达式（可以是 Identifier、ArrayLiteralExpression 或任意表达式）</param>
    /// <param name="index">索引表达式</param>
    public ArrayIndexExpression(LogicalExpression array, LogicalExpression index) {
        Array = array;
        Index = index;
    }

    /// <summary>
    /// 获取数组表达式
    /// </summary>
    public LogicalExpression Array { get; }

    /// <summary>
    /// 获取索引表达式
    /// </summary>
    public LogicalExpression Index { get; }

    /// <inheritdoc />
    public override void Accept(IExpressionVisitor visitor) {
        visitor.Visit(this);
    }

    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) {
        return visitor.Visit(this);
    }
}
