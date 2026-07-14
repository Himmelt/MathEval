using MathEval.Visitors;

namespace MathEval.AST;

/// <summary>
/// 表示数组索引表达式，如 arr[0]、arr[i]、(arr*2)[3]
/// </summary>
public sealed class ArrayIndexExpression : LogicalExpression {
    /// <summary>
    /// 初始化 ArrayIndexExpression 类的新实例
    /// </summary>
    /// <param name="array">数组表达式（可以是 Identifier、ArrayLiteralExpression 或任意表达式）</param>
    /// <param name="index">索引表达式</param>
    /// <param name="isSynthetic">是否为优化器生成的合成索引（非用户原始编写）</param>
    public ArrayIndexExpression(LogicalExpression array, LogicalExpression index, bool isSynthetic = false) {
        Array = array;
        Index = index;
        IsSynthetic = isSynthetic;
    }

    /// <summary>
    /// 获取数组表达式
    /// </summary>
    public LogicalExpression Array { get; }

    /// <summary>
    /// 获取索引表达式
    /// </summary>
    public LogicalExpression Index { get; }

    /// <summary>
    /// 获取一个值，指示此索引是否为 IndexPushdownOptimizer 生成的合成索引。
    /// 合成索引对标量值静默返回标量本身（用于混合标量/数组运算的优化）；
    /// 用户原始编写的索引对标量值则抛出类型错误，提供清晰的错误反馈。
    /// </summary>
    public bool IsSynthetic { get; }

    /// <inheritdoc />
    public override void Accept(IExpressionVisitor visitor) {
        visitor.Visit(this);
    }

    /// <inheritdoc />
    public override T Accept<T>(IExpressionVisitor<T> visitor) {
        return visitor.Visit(this);
    }
}
